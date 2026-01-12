import React from 'react';
import { GitBranch } from 'lucide-react';
import CircularScore from './CircularScore';

const AnalysisHeroCard = ({ result, score, filteredIssues, extractRepoName, repoUrl, branchName }) => {
    return (
        <div className="relative overflow-hidden bg-gradient-to-br from-slate-900 via-slate-800 to-indigo-950 rounded-2xl shadow-xl border border-white/10 p-8 text-white">
            <div className="absolute top-0 right-0 -mt-20 -mr-20 w-96 h-96 bg-indigo-500/20 rounded-full blur-3xl"></div>
            <div className="absolute bottom-0 left-0 -mb-20 -ml-20 w-80 h-80 bg-blue-500/10 rounded-full blur-3xl"></div>

            <div className="relative z-10 flex flex-col lg:flex-row items-center justify-between gap-8">
                <div className="flex-1 space-y-4">
                    <div className="flex items-center gap-3 text-indigo-200 text-sm font-medium tracking-wide uppercase">
                        <GitBranch className="w-4 h-4" />
                        <span>Branch Analysis</span>
                    </div>

                    <div>
                        <div className="flex flex-wrap items-baseline gap-3 mb-2">
                            <h3 className="text-3xl font-bold text-white tracking-tight">
                                {result.repoName || extractRepoName(repoUrl) || 'Repository'}
                            </h3>
                            <span className="px-3 py-1 bg-white/10 rounded-full text-indigo-100 text-sm font-mono border border-white/10">
                                @{result.branchName || branchName}
                            </span>
                        </div>
                        <p className="text-indigo-200/80 max-w-2xl text-sm leading-relaxed">
                            {result.summary}
                        </p>
                    </div>

                    <div className="flex items-center gap-4 pt-2">
                        <div className="flex flex-col px-4 py-2 rounded-lg bg-white/5 border border-white/10 backdrop-blur-sm">
                            <span className="text-2xl font-bold text-white leading-none">{filteredIssues.length}</span>
                            <span className="text-[10px] text-indigo-200 uppercase tracking-wider mt-1">Issues</span>
                        </div>
                        <div className="flex flex-col px-4 py-2 rounded-lg bg-white/5 border border-white/10 backdrop-blur-sm">
                            <span className="text-2xl font-bold text-white leading-none">
                                {result.filesAnalyzed || 0}
                            </span>
                            <span className="text-[10px] text-indigo-200 uppercase tracking-wider mt-1">Files</span>
                        </div>
                    </div>
                </div>

                <div className="flex items-center gap-8">
                    <div className="h-20 w-px bg-gradient-to-b from-transparent via-white/20 to-transparent hidden lg:block"></div>
                    <div className="flex flex-col items-center">
                        <CircularScore score={score} size="lg" />
                    </div>
                </div>
            </div>
        </div>
    );
};

export default AnalysisHeroCard;
