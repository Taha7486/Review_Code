import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { analysisService } from '../services/api';

// Query keys
export const analysisKeys = {
    all: ['analyses'],
    runs: (repoUrl, branchName, limit) => [...analysisKeys.all, 'runs', { repoUrl, branchName, limit }],
    run: (id) => [...analysisKeys.all, 'run', id],
    branches: (repoUrl, githubToken) => [...analysisKeys.all, 'branches', { repoUrl, githubToken }],
    issues: (runId, severity, category) => [...analysisKeys.all, 'issues', runId, { severity, category }],
};

/**
 * Hook to fetch analysis runs
 */
export const useAnalysisRuns = (repoUrl = null, branchName = null, limit = 20) => {
    return useQuery({
        queryKey: analysisKeys.runs(repoUrl, branchName, limit),
        queryFn: () => analysisService.getAnalysisRuns(repoUrl, branchName, limit),
        enabled: true, // Always enabled
    });
};

/**
 * Hook to fetch a single analysis run by ID
 * NO CACHING - always fetches fresh data
 */
export const useAnalysisRun = (runId, options = {}) => {
    return useQuery({
        queryKey: analysisKeys.run(runId),
        queryFn: () => analysisService.getAnalysisRunById(runId),
        enabled: !!runId && (options.enabled !== false),
        staleTime: 0, // ALWAYS stale - refetch every time
        gcTime: 0, // Don't cache - remove immediately after unmount
        cacheTime: 0, // Legacy support
        retry: 1, // Only retry once to avoid delays
        refetchOnMount: true, // Always refetch on mount
        refetchOnWindowFocus: false, // Don't refetch on window focus
        refetchOnReconnect: false, // Don't refetch on reconnect
        refetchInterval: (query) => {
            const data = query.state.data;
            // Poll every 2 seconds if status is 'running' or 'queued'
            if (!data || data.status === 'running' || data.status === 'queued') {
                return 2000;
            }
            return false;
        },
        ...options,
    });
};

/**
 * Hook to fetch branches for a repository
 */
export const useBranches = (repoUrl, githubToken = null, options = {}) => {
    return useQuery({
        queryKey: analysisKeys.branches(repoUrl, githubToken),
        queryFn: () => analysisService.getBranches(repoUrl, githubToken),
        enabled: !!repoUrl && (options.enabled !== false),
        retry: false, // Don't retry on 404/401
        ...options,
    });
};

/**
 * Hook to fetch issues for an analysis run
 */
export const useAnalysisIssues = (runId, severity = null, category = null, options = {}) => {
    return useQuery({
        queryKey: analysisKeys.issues(runId, severity, category),
        queryFn: () => analysisService.getAnalysisRunIssues(runId, severity, category),
        enabled: !!runId && (options.enabled !== false),
        ...options,
    });
};

/**
 * Hook to start an analysis (mutation)
 */
export const useStartAnalysis = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({ repoUrl, branchName, githubToken }) =>
            analysisService.analyzeBranch(repoUrl, branchName, githubToken),
        onMutate: async () => {
            // Cancel any outgoing refetches to avoid race conditions
            await queryClient.cancelQueries({ queryKey: analysisKeys.all });
        },
        onSuccess: (data) => {
            // Correctly invalidate ALL runs lists by using the base runs key
            queryClient.invalidateQueries({ queryKey: ['analyses', 'runs'] });

            // If we have a runId, invalidate its cache immediately to force fresh fetch
            if (data?.runId) {
                queryClient.invalidateQueries({ queryKey: analysisKeys.run(data.runId) });
                queryClient.removeQueries({ queryKey: analysisKeys.run(data.runId) });
            }

            // Let the polling in useAnalysisRun fetch fresh data from API
            // Don't pre-populate cache - it can cause structure mismatches
        },
    });
};
