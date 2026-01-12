import React, { useState, useEffect } from 'react';
import { AlertTriangle, XCircle, RefreshCw, GitBranch, Zap, Info } from 'lucide-react';
import { useBranches, useStartAnalysis } from '../../hooks/useAnalysis';
import { useDebounce } from '../../hooks/useDebounce';

const ChevronDownIcon = ({ className }) => (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
    </svg>
);

const AnalysisForm = ({ onAnalysisStart }) => {
    const [repoUrl, setRepoUrl] = useState('');
    const [branchName, setBranchName] = useState('');
    const [branches, setBranches] = useState([]);
    const [error, setError] = useState(null);
    const [showTokenInput, setShowTokenInput] = useState(false);
    const [githubToken, setGithubToken] = useState('');
    const [repoError, setRepoError] = useState('');

    const debouncedRepoUrl = useDebounce(repoUrl, 500);

    const { data: branchesData, isLoading: fetchingBranches, error: branchesError, refetch: refetchBranches } = useBranches(
        debouncedRepoUrl,
        showTokenInput ? githubToken : null,
        { enabled: !!debouncedRepoUrl && debouncedRepoUrl.length > 0 }
    );
    const startAnalysisMutation = useStartAnalysis();

    useEffect(() => {
        if (branchesData) {
            setBranches(branchesData);
            if (branchesData.length > 0 && !branchName) {
                setBranchName(branchesData[0]);
            }
            setShowTokenInput(false);
            setRepoError('');
        }
    }, [branchesData, branchName]);

    useEffect(() => {
        if (branchesError) {
            const status = branchesError.status;
            if (status === 404 || status === 403) {
                if (!showTokenInput) {
                    setShowTokenInput(true);
                    setRepoError('Repository not found. If this is a private repository, please enter your GitHub Personal Access Token with "repo" scope.');
                } else {
                    setRepoError('Failed to access repository with provided token. Please check your token has "repo" scope and the URL is correct.');
                }
            } else {
                setRepoError(branchesError.message || 'Failed to fetch branches. Check URL and try again.');
            }
        }
    }, [branchesError, showTokenInput]);

    const fetchBranches = async (useToken = false) => {
        if (!repoUrl) return;
        setRepoError('');
        refetchBranches();
    };

    const handleAnalyze = async (e) => {
        e.preventDefault();
        setError(null);

        try {
            const tokenToUse = showTokenInput ? githubToken : null;
            const response = await startAnalysisMutation.mutateAsync({
                repoUrl,
                branchName,
                githubToken: tokenToUse
            });

            if (response.runId) {
                onAnalysisStart(response.runId, repoUrl, branchName);
            }
        } catch (err) {
            setError(err.message || 'Analysis failed. Please try again.');
        }
    };

    return (
        <div className="max-w-2xl mx-auto mt-10">
            <div className="text-center mb-10">
                <h2 className="text-3xl font-bold text-gray-900 dark:text-white mb-4">Start a New Analysis</h2>
                <p className="text-gray-500 dark:text-gray-400 text-lg">
                    paste a GitHub repository URL to automatically detect issues and get code quality scores.
                </p>
            </div>

            <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl border border-gray-100 dark:border-gray-700 p-8">
                <form onSubmit={handleAnalyze} className="space-y-6">
                    <div className="space-y-2">
                        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">
                            Repository URL
                        </label>
                        <div className="relative">
                            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                                <GitBranch className="h-5 w-5 text-gray-400" />
                            </div>
                            <input
                                type="text"
                                required
                                placeholder="https://github.com/username/repository"
                                value={repoUrl}
                                onChange={(e) => {
                                    setRepoUrl(e.target.value);
                                    setBranchName('');
                                    setBranches([]);
                                    setShowTokenInput(false);
                                    setRepoError('');
                                }}
                                className="block w-full pl-11 pr-12 py-3 border border-gray-300 dark:border-gray-600 rounded-xl bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all"
                            />
                            <button
                                type="button"
                                onClick={() => fetchBranches(false)}
                                disabled={fetchingBranches || !repoUrl}
                                className="absolute right-2 top-1/2 transform -translate-y-1/2 p-2 text-gray-500 hover:text-indigo-600 dark:hover:text-indigo-400 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors disabled:opacity-50"
                                title="Load Branches"
                            >
                                <RefreshCw className={`w-5 h-5 ${fetchingBranches ? 'animate-spin' : ''}`} />
                            </button>
                        </div>
                        {repoError && (
                            <p className="text-sm text-red-500 mt-1 flex items-center gap-1">
                                <AlertTriangle className="w-4 h-4" />
                                {repoError}
                            </p>
                        )}
                    </div>

                    {repoUrl && (
                        <div className="space-y-2">
                            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">
                                Branch
                            </label>
                            {branches.length > 0 ? (
                                <div className="relative">
                                    <select
                                        value={branchName}
                                        onChange={(e) => setBranchName(e.target.value)}
                                        className="block w-full px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-xl bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 appearance-none transition-all"
                                    >
                                        {branches.map((b) => (
                                            <option key={b} value={b}>{b}</option>
                                        ))}
                                    </select>
                                    <ChevronDownIcon className="absolute right-4 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
                                </div>
                            ) : showTokenInput ? (
                                <div className="space-y-3 animate-fadeIn">
                                    <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-3 flex items-start gap-2">
                                        <Info className="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" />
                                        <div className="text-sm text-blue-800 dark:text-blue-300">
                                            <p className="font-medium mb-1">GitHub Personal Access Token Required</p>
                                            <p className="text-xs">Your token needs the <code className="bg-blue-100 dark:bg-blue-900/50 px-1 rounded">repo:read</code> scope to access private repositories. Create one at <a href="https://github.com/settings/tokens" target="_blank" rel="noopener noreferrer" className="underline">github.com/settings/tokens</a></p>
                                        </div>
                                    </div>
                                    <input
                                        type="password"
                                        placeholder="Enter GitHub Personal Access Token"
                                        value={githubToken}
                                        onChange={(e) => setGithubToken(e.target.value)}
                                        className="block w-full px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-xl bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all"
                                    />
                                    <button
                                        type="button"
                                        onClick={() => fetchBranches(true)}
                                        disabled={fetchingBranches || !githubToken}
                                        className="w-full py-2 bg-gray-800 dark:bg-gray-700 text-white rounded-lg hover:bg-gray-900 dark:hover:bg-gray-600 transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                                    >
                                        {fetchingBranches ? 'Fetching...' : 'Retry Access with Token'}
                                    </button>
                                </div>
                            ) : fetchingBranches ? (
                                <div className="flex items-center justify-center py-3 text-gray-500 dark:text-gray-400">
                                    <RefreshCw className="w-5 h-5 animate-spin mr-2" />
                                    <span>Loading branches...</span>
                                </div>
                            ) : null}
                        </div>
                    )}

                    <button
                        type="submit"
                        disabled={startAnalysisMutation.isPending || !repoUrl || !branchName}
                        className="w-full py-3 bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-700 hover:to-violet-700 text-white rounded-xl font-bold shadow-lg hover:shadow-xl transform hover:-translate-y-0.5 transition-all disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none flex items-center justify-center gap-2"
                    >
                        {startAnalysisMutation.isPending ? (
                            <>
                                <RefreshCw className="w-5 h-5 animate-spin" />
                                Starting Analysis...
                            </>
                        ) : (
                            <>
                                <Zap className="w-5 h-5" />
                                Start Analysis
                            </>
                        )}
                    </button>

                    {error && (
                        <div className="p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-900 rounded-xl flex items-center gap-3 text-red-600 dark:text-red-400">
                            <XCircle className="w-5 h-5 flex-shrink-0" />
                            <p className="text-sm font-medium">{error}</p>
                        </div>
                    )}
                </form>
            </div>
        </div>
    );
};

export default AnalysisForm;
