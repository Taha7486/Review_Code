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

    // Track seen deep nesting messages to avoid duplicates
    const seenDeepNestingMessages = new Set();

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
        <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-sm transition-all duration-300 flex flex-col h-[calc(100vh-250px)]">
            {/* Header */}
            <div className="bg-gray-50/80 dark:bg-gray-900/80 backdrop-blur-sm px-4 py-3 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between sticky top-0 z-30">
                <div className="flex items-center gap-3">
                    <FileIcon filename={selectedFile} />
                    <span className="font-mono text-sm font-semibold text-gray-900 dark:text-white truncate max-w-[200px] md:max-w-md">{selectedFile}</span>
                    <span className="h-4 w-px bg-gray-300 dark:bg-gray-600 hidden sm:block"></span>
                    <span className="text-xs text-gray-500 dark:text-gray-400 hidden sm:block">
                        {fileIssues.length} issue{fileIssues.length !== 1 ? 's' : ''}
                    </span>
                </div>
                <div className="flex items-center gap-2">
                    {fileIssues.length > 0 && (
                        <div className="flex items-center gap-2 bg-white dark:bg-gray-800 rounded-lg p-1 border border-gray-200 dark:border-gray-700 shadow-sm ml-2">
                            <button
                                onClick={() => onJumpToIssue('prev')}
                                className="p-1.5 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md transition-colors text-gray-600 dark:text-gray-300"
                                title="Previous issue"
                            >
                                <ArrowUp className="w-4 h-4" />
                            </button>
                            <span className="text-xs font-mono font-bold text-indigo-600 dark:text-indigo-400 min-w-[3.5rem] text-center">
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
            </div>

            {/* Code View */}
            <div ref={codeViewerRef} className="relative flex-1 overflow-auto custom-scrollbar bg-[#0d1117]">
                <div className="p-4 text-sm font-mono leading-relaxed min-w-max">
                    {fileContent.split('\n').map((line, index) => {
                        const lineNum = index + 1;
                        const lineIssues = fileIssues.filter(i => (i.line || i.Line) === lineNum);
                        const isLineTargeted = currentIssue && (currentIssue.line || currentIssue.Line) === lineNum;

                        const severities = lineIssues.map(i => (i.severity || i.Severity || '').toLowerCase());
                        let lineSevColor = 'border-blue-500/50 bg-blue-500/10';
                        if (severities.includes('critical')) lineSevColor = 'border-red-500/50 bg-red-500/10';
                        else if (severities.includes('major') || severities.includes('medium')) lineSevColor = 'border-orange-500/50 bg-orange-500/10';
                        else if (severities.includes('minor') || severities.includes('low')) lineSevColor = 'border-yellow-500/50 bg-yellow-500/10';

                        return (
                            <React.Fragment key={index}>
                                <div
                                    ref={el => {
                                        if (lineIssues.length > 0) {
                                            const file = (lineIssues[0].file || lineIssues[0].File || lineIssues[0].filePath || lineIssues[0].FilePath || selectedFile).toLowerCase();
                                            const lineNumber = lineIssues[0].line || lineIssues[0].Line || lineNum;
                                            issueRefs.current[`${file}-${lineNumber}`] = el;
                                        }
                                    }}
                                    className={`flex group hover:bg-gray-800/80 transition-colors ${isLineTargeted
                                        ? 'border-l-[4px] border-l-indigo-500 bg-indigo-500/20 z-10 relative'
                                        : lineIssues.length > 0
                                            ? `border-l-[4px] ${lineSevColor.split(' ')[0]} ${lineSevColor.split(' ')[1]}`
                                            : 'border-l-[4px] border-transparent'
                                        }`}
                                >
                                    <div className={`w-12 flex-shrink-0 text-right pr-4 select-none opacity-50 ${isLineTargeted ? 'text-indigo-400 font-bold opacity-100' : lineIssues.length > 0 ? 'text-yellow-500 opacity-100' : 'text-gray-600'
                                        }`}>
                                        {lineNum}
                                    </div>
                                    <div className={`flex-1 pl-4 whitespace-pre ${isLineTargeted ? 'text-white font-medium' : 'text-gray-300'
                                        }`}>
                                        {line || ' '}
                                    </div>
                                </div>

                                {/* Inline Issue Messages */}
                                {lineIssues.map((issue, idx) => {
                                    const message = issue.message || issue.Message || '';
                                    const isDeepNesting = message.toLowerCase().includes('deeply nested') || message.toLowerCase().includes('nesting');

                                    // Deduplicate Deep Nesting logic
                                    if (isDeepNesting) {
                                        if (seenDeepNestingMessages.has(message)) return null;
                                        seenDeepNestingMessages.add(message);
                                    }

                                    const isCurrent = currentIssue &&
                                        (currentIssue.line || currentIssue.Line) === (issue.line || issue.Line) &&
                                        (currentIssue.message || currentIssue.Message) === (issue.message || issue.Message);

                                    const sev = (issue.severity || issue.Severity || '').toLowerCase();

                                    return (
                                        <div
                                            key={idx}
                                            className={`ml-16 mr-8 my-2 p-3 rounded-xl border text-sm font-sans whitespace-normal transition-all duration-300 shadow-xl ${sev === 'critical' ? 'bg-red-950/40 border-red-500/50 text-red-100' :
                                                (sev === 'major' || sev === 'medium') ? 'bg-orange-950/40 border-orange-500/50 text-orange-100' :
                                                    'bg-yellow-950/40 border-yellow-500/50 text-yellow-100'
                                                } ${isCurrent ? 'ring-2 ring-indigo-500 scale-[1.01] z-20 relative shadow-indigo-500/20' : ''}`}
                                        >
                                            <div className="flex items-center gap-2 mb-2">
                                                <div className={`p-1 rounded-md ${sev === 'critical' ? 'bg-red-500/20' :
                                                    (sev === 'major' || sev === 'medium') ? 'bg-orange-500/20' :
                                                        'bg-yellow-500/20'
                                                    }`}>
                                                    <AlertTriangle className="w-3.5 h-3.5" />
                                                </div>
                                                <span className="font-black uppercase tracking-widest text-[10px] opacity-70">{sev}</span>
                                                <span className="w-1.5 h-1.5 rounded-full bg-white/10"></span>
                                                <span className="font-bold text-xs text-white/80">{issue.category || issue.Category}</span>
                                            </div>
                                            <div className="font-medium text-[13px] leading-relaxed">
                                                {message}
                                            </div>
                                            {(issue.rule || issue.Rule) && (
                                                <div className="mt-2 pt-2 border-t border-white/5 text-[10px] font-mono opacity-50 flex items-center gap-1.5">
                                                    <span className="font-bold opacity-40">RULE</span> {issue.rule || issue.Rule}
                                                </div>
                                            )}
                                        </div>
                                    );
                                })}
                            </React.Fragment>
                        );
                    })}
                </div>
            </div>

            {/* Issue Details Footer */}
            {fileIssues.length > 0 && currentIssue && (
                <div className="border-t border-gray-200 dark:border-gray-700 bg-gray-50/95 dark:bg-gray-900/95 backdrop-blur-md p-4 transition-all">
                    <div className="flex items-start gap-4 animate-fadeIn">
                        <div className={`p-2 rounded-lg ${currentIssue.severity === 'critical' ? 'bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400' :
                            (currentIssue.severity === 'major' || currentIssue.severity === 'medium') ? 'bg-orange-100 text-orange-600 dark:bg-orange-900/30 dark:text-orange-400' :
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
