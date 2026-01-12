import React, { useRef } from 'react';
import { FileText, ArrowUp, ArrowDown, AlertTriangle } from 'lucide-react';
import FileIcon from './FileIcon';
import SeverityBadge from './SeverityBadge';

const CodeInspector = ({
    selectedFile,
    fileContent,
    fileIssues,
    currentIssueIndex,
    onJumpToIssue,
    issueRefs
}) => {
    const codeViewerRef = useRef(null);

    if (!selectedFile || !fileContent) {
        return (
            <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-12 text-center transition-colors h-[calc(100vh-250px)] flex flex-col items-center justify-center">
                <div className="w-20 h-20 bg-gray-100 dark:bg-gray-700/50 rounded-full flex items-center justify-center mb-6">
                    <FileText className="w-10 h-10 text-gray-400 dark:text-gray-500" />
                </div>
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">Select a file to view detailed analysis</h3>
                <p className="text-gray-500 dark:text-gray-400 max-w-sm mx-auto">
                    Click on any file from the list on the left to see code issues line by line.
                </p>
            </div>
        );
    }

    const currentIssue = fileIssues[currentIssueIndex];

    return (
        <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-sm transition-colors flex flex-col h-[calc(100vh-250px)]">
            {/* Header */}
            <div className="bg-gray-50/80 dark:bg-gray-900/80 backdrop-blur-sm px-4 py-3 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between sticky top-0 z-10">
                <div className="flex items-center gap-3">
                    <FileIcon filename={selectedFile} />
                    <span className="font-mono text-sm font-semibold text-gray-900 dark:text-white">{selectedFile}</span>
                    <span className="h-4 w-px bg-gray-300 dark:bg-gray-600"></span>
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                        {fileIssues.length} issue{fileIssues.length !== 1 ? 's' : ''}
                    </span>
                </div>
                {fileIssues.length > 0 && (
                    <div className="flex items-center gap-2 bg-white dark:bg-gray-800 rounded-lg p-1 border border-gray-200 dark:border-gray-700 shadow-sm">
                        <button
                            onClick={() => onJumpToIssue('prev')}
                            className="p-1.5 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md transition-colors text-gray-600 dark:text-gray-300"
                            title="Previous issue"
                        >
                            <ArrowUp className="w-4 h-4" />
                        </button>
                        <span className="text-xs font-mono font-medium text-gray-600 dark:text-gray-400 min-w-[3rem] text-center">
                            {currentIssueIndex + 1} / {fileIssues.length}
                        </span>
                        <button
                            onClick={() => onJumpToIssue('next')}
                            className="p-1.5 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md transition-colors text-gray-600 dark:text-gray-300"
                            title="Next issue"
                        >
                            <ArrowDown className="w-4 h-4" />
                        </button>
                    </div>
                )}
            </div>

            {/* Code View */}
            <div ref={codeViewerRef} className="relative flex-1 overflow-auto custom-scrollbar bg-[#0d1117]">
                <div className="p-4 text-sm font-mono leading-relaxed min-w-max">
                    {fileContent.split('\n').map((line, index) => {
                        const lineNum = index + 1;
                        const issue = fileIssues.find(i => i.line === lineNum);
                        const isCurrentIssue = issue && currentIssue && currentIssue.line === lineNum;

                        const lineIssues = fileIssues.filter(i => i.line === lineNum);
                        const severities = lineIssues.map(i => (i.severity || '').toLowerCase());
                        let lineSevColor = 'border-blue-500/50 bg-blue-500/10';
                        if (severities.includes('critical')) lineSevColor = 'border-red-500/50 bg-red-500/10';
                        else if (severities.includes('major')) lineSevColor = 'border-orange-500/50 bg-orange-500/10';
                        else if (severities.includes('minor')) lineSevColor = 'border-yellow-500/50 bg-yellow-500/10';

                        return (
                            <div
                                key={index}
                                ref={el => {
                                    if (issue) {
                                        const file = issue.file || selectedFile;
                                        const lineNumber = issue.line || lineNum;
                                        issueRefs.current[`${file}-${lineNumber}`] = el;
                                    }
                                }}
                                className={`flex group hover:bg-gray-800/50 transition-colors ${isCurrentIssue
                                    ? 'border-l-[3px] border-l-blue-400 bg-blue-500/20 shadow-[0_0_15px_rgba(59,130,246,0.1)] z-10 relative'
                                    : issue
                                        ? `border-l-[3px] ${lineSevColor.split(' ')[0]} ${lineSevColor.split(' ')[1]}`
                                        : 'border-l-[3px] border-transparent'
                                    }`}
                            >
                                <div className={`w-12 flex-shrink-0 text-right pr-4 select-none ${isCurrentIssue ? 'text-blue-300 font-bold' : issue ? 'text-yellow-500' : 'text-gray-600'
                                    }`}>
                                    {lineNum}
                                </div>
                                <div className={`flex-1 pl-4 whitespace-pre ${isCurrentIssue ? 'text-white font-medium' : 'text-gray-300'
                                    }`}>
                                    {line || ' '}
                                </div>
                                {issue && (
                                    <div className="absolute right-4 px-2 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider opacity-0 group-hover:opacity-100 transition-opacity bg-black/50 text-white backdrop-blur-sm border border-white/10">
                                        {issue.severity}
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>

            {/* Issue Details Footer */}
            {fileIssues.length > 0 && currentIssue && (
                <div className="border-t border-gray-200 dark:border-gray-700 bg-gray-50/95 dark:bg-gray-900/95 backdrop-blur-md p-4 transition-all">
                    <div className="flex items-start gap-4 animate-fadeIn">
                        <div className={`p-2 rounded-lg ${currentIssue.severity === 'critical' ? 'bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400' :
                            currentIssue.severity === 'major' ? 'bg-orange-100 text-orange-600 dark:bg-orange-900/30 dark:text-orange-400' :
                                'bg-yellow-100 text-yellow-600 dark:bg-yellow-900/30 dark:text-yellow-400'
                            }`}>
                            <AlertTriangle className="w-5 h-5" />
                        </div>
                        <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1.5 align-baseline">
                                <SeverityBadge severity={currentIssue.severity} />
                                <span className="text-xs font-mono text-gray-500 dark:text-gray-400">
                                    Line {currentIssue.line}
                                </span>
                                <span className="h-1 w-1 rounded-full bg-gray-400"></span>
                                <span className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide">
                                    {currentIssue.category}
                                </span>
                            </div>
                            <p className="text-gray-900 dark:text-white font-medium text-sm leading-relaxed">
                                {currentIssue.message}
                            </p>
                            {currentIssue.rule && (
                                <p className="text-xs text-gray-500 dark:text-gray-500 mt-1.5 font-mono bg-gray-100 dark:bg-gray-800 inline-block px-1.5 py-0.5 rounded border border-gray-200 dark:border-gray-700">
                                    Rule: {currentIssue.rule}
                                </p>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default CodeInspector;
