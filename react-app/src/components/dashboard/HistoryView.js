import React from 'react';
import { Clock, GitBranch, RefreshCw, ChevronRight } from 'lucide-react';
import { useAnalysisRuns } from '../../hooks/useAnalysis';

const HistoryView = ({ onSelectAnalysis }) => {
    const { data: recentAnalyses = [], isLoading: loadingHistory, refetch: refetchRuns } = useAnalysisRuns();

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Recent Analyses</h2>
                <button onClick={refetchRuns} className="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors">
                    <RefreshCw className={`w-5 h-5 text-gray-500 ${loadingHistory ? 'animate-spin' : ''}`} />
                </button>
            </div>

            <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 overflow-hidden">
                {recentAnalyses.length > 0 ? (
                    <div className="divide-y divide-gray-200 dark:divide-gray-700">
                        {recentAnalyses.map((item) => (
                            <button
                                key={item.id}
                                onClick={() => onSelectAnalysis(item.id, item.repoName, item.branchName)}
                                className="w-full text-left p-6 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors flex items-center justify-between group"
                            >
                                <div className="flex items-center gap-4">
                                    <div className="p-3 bg-indigo-50 dark:bg-indigo-900/20 rounded-lg group-hover:bg-indigo-100 dark:group-hover:bg-indigo-900/40 transition-colors">
                                        <GitBranch className="w-6 h-6 text-indigo-600 dark:text-indigo-400" />
                                    </div>
                                    <div>
                                        <h3 className="font-semibold text-gray-900 dark:text-white text-lg">
                                            {item.repoName}
                                            <span className="text-gray-400 mx-2">/</span>
                                            <span className="text-indigo-600 dark:text-indigo-400 font-mono text-sm bg-indigo-50 dark:bg-indigo-900/30 px-2 py-0.5 rounded">
                                                {item.branchName}
                                            </span>
                                        </h3>
                                        <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                                            {new Date(item.createdAt).toLocaleDateString()} at {new Date(item.createdAt).toLocaleTimeString()}
                                        </p>
                                    </div>
                                </div>
                                <div className="flex items-center gap-6">
                                    <div className="text-right">
                                        <div className="text-sm text-gray-500 dark:text-gray-400 mb-0.5">Score</div>
                                        <div className={`font-bold text-lg ${item.averageScore >= 80 ? 'text-green-500' :
                                            item.averageScore < 50 ? 'text-red-500' : 'text-yellow-500'
                                            }`}>
                                            {item.averageScore}
                                        </div>
                                    </div>
                                    <ChevronRight className="w-5 h-5 text-gray-400 group-hover:text-indigo-500 transition-colors" />
                                </div>
                            </button>
                        ))}
                    </div>
                ) : (
                    <div className="p-12 text-center text-gray-500 dark:text-gray-400">
                        <Clock className="w-12 h-12 mx-auto mb-4 opacity-20" />
                        No history available yet.
                    </div>
                )}
            </div>
        </div>
    );
};

export default HistoryView;
