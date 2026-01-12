import React, { useState, useEffect } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useAnalysisRun } from '../../hooks/useAnalysis';
import { AnalysisView } from '../analysis';
import { useQueryClient } from '@tanstack/react-query';

const extractScore = (summary) => {
    if (!summary) return 0;
    const match = summary.match(/Score: (\d+)/);
    return match ? parseInt(match[1]) : 0;
};

const extractRepoName = (url) => {
    if (!url) return '';
    try {
        const parts = url.split('/');
        return parts.length >= 2 ? `${parts[parts.length - 2]}/${parts[parts.length - 1]}` : url;
    } catch (e) {
        return url;
    }
};

const ResultsView = ({ runId, repoUrl, branchName, onStartNewAnalysis }) => {
    const [result, setResult] = useState(null);
    const queryClient = useQueryClient();
    const { data: analysisRun, isLoading: loadingDetails, error: queryError } = useAnalysisRun(runId, {
        enabled: !!runId,
    });
    useEffect(() => {
        // Reset local result when switching to a different runId
        // This prevents showing old results while a new one is loading/running
        setResult(null);
    }, [runId]);

    useEffect(() => {
        if (runId && analysisRun) {
            if (process.env.NODE_ENV === 'development') {
                console.log(`[ResultsView] Run ${runId} status changed to: ${analysisRun.status}`);
            }

            if (analysisRun.status === 'completed' || analysisRun.status === 'failed') {
                setResult(analysisRun);

                // 🚀 Sync the history list: If an analysis just finished, 
                // invalidate the plural list so it shows the new score instead of 0
                queryClient.invalidateQueries({ queryKey: ['analyses', 'runs'] });
            }
        }

        if (queryError) {
            console.error('Error fetching analysis run:', queryError);
        }
    }, [runId, analysisRun, queryError, queryClient]);

    const score = result ? (result.averageScore || extractScore(result.summary)) : 0;

    // Detailed debugging (Dev only)
    if (process.env.NODE_ENV === 'development') {
        console.log('ResultsView Render State:', {
            runId,
            hasAnalysisRun: !!analysisRun,
            analysisRunStatus: analysisRun?.status,
            loadingDetails,
            hasResult: !!result,
            hasQueryError: !!queryError
        });
    }

    // Show loading only if we're actually loading OR if status is 'running'
    const isRunning = analysisRun?.status === 'running';
    const shouldShowLoading = (loadingDetails && !analysisRun) || isRunning;

    if (shouldShowLoading) {
        return (
            <div className="space-y-6">
                <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-12 text-center animate-pulse">
                    <div className="flex flex-col items-center gap-4">
                        <div className="w-12 h-12 border-4 border-indigo-600 border-t-transparent rounded-full animate-spin"></div>
                        <div>
                            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
                                {isRunning ? 'Analysis in Progress...' : 'Loading Analysis...'}
                            </h3>
                            <p className="text-gray-500 dark:text-gray-400">
                                {isRunning ? 'Your code is being analyzed. This may take a few moments...' : 'Fetching analysis details and file contents...'}
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    if (result) {
        if (process.env.NODE_ENV === 'development') {
            console.log('Rendering AnalysisView with result');
        }
        return (
            <AnalysisView
                result={result}
                repoUrl={repoUrl}
                branchName={branchName}
                score={score}
                extractRepoName={extractRepoName}
            />
        );
    }

    return (
        <div className="space-y-6">
            <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-12 text-center">
                <div className="flex flex-col items-center gap-4">
                    <AlertTriangle className="w-16 h-16 text-yellow-500" />
                    <div>
                        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">No Results Available</h3>
                        <p className="text-gray-500 dark:text-gray-400 mb-4">
                            No analysis results found. Please start a new analysis.
                        </p>
                        <button
                            onClick={onStartNewAnalysis}
                            className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 transition-colors shadow-md hover:shadow-lg"
                        >
                            Start New Analysis
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ResultsView;
