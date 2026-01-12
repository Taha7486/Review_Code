import React from 'react';
import { FileText } from 'lucide-react';
import FileIcon from './FileIcon';

const FileListSidebar = ({ uniqueFiles, filteredIssues, selectedFile, onSelectFile }) => {
    return (
        <div className="lg:col-span-1 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 flex flex-col max-h-[calc(100vh-250px)] sticky top-40 shadow-sm transition-colors">
            <div className="p-4 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center bg-gray-50/50 dark:bg-gray-900/50 rounded-t-xl">
                <h4 className="font-semibold text-gray-900 dark:text-white flex items-center gap-2 text-sm uppercase tracking-wide">
                    <FileText className="w-4 h-4 text-gray-500" />
                    Files
                </h4>
                <span className="bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-0.5 rounded-full text-xs font-medium">
                    {uniqueFiles.length}
                </span>
            </div>

            <div className="flex-1 overflow-y-auto p-2 space-y-1 custom-scrollbar">
                {uniqueFiles.length === 0 ? (
                    <div className="p-8 text-center text-gray-500 dark:text-gray-400">
                        <FileText className="w-12 h-12 mx-auto mb-3 opacity-50" />
                        <p className="text-sm">No files found</p>
                    </div>
                ) : (
                    uniqueFiles.map(file => {
                        const issuesInFile = filteredIssues.filter(i => {
                            const issueFile = (i.filePath || i.FilePath || i.file || i.File || '').toLowerCase();
                            return issueFile === file.toLowerCase();
                        });
                        const issueCount = issuesInFile.length;
                        const severities = issuesInFile.map(i => ((i.severity || i.Severity || 'info')).toLowerCase());
                        let worstSev = 'info';
                        if (severities.includes('critical')) worstSev = 'critical';
                        else if (severities.includes('major')) worstSev = 'major';
                        else if (severities.includes('minor')) worstSev = 'minor';

                        const sevColor = {
                            critical: 'text-red-500 bg-red-50 dark:bg-red-900/20',
                            major: 'text-orange-500 bg-orange-50 dark:bg-orange-900/20',
                            minor: 'text-yellow-600 bg-yellow-50 dark:bg-yellow-900/20',
                            info: 'text-blue-500 bg-blue-50 dark:bg-blue-900/20'
                        }[worstSev];

                        return (
                            <button
                                key={file}
                                onClick={() => onSelectFile(file)}
                                className={`w-full text-left px-3 py-2.5 rounded-lg transition-all border group ${selectedFile === file
                                    ? 'bg-indigo-50 dark:bg-indigo-900/30 border-indigo-200 dark:border-indigo-800 shadow-sm'
                                    : 'border-transparent hover:bg-gray-50 dark:hover:bg-gray-700/50'
                                    }`}
                            >
                                <div className="flex items-center justify-between gap-3">
                                    <div className="flex items-center gap-3 min-w-0">
                                        <FileIcon filename={file} />
                                        <span className={`text-sm font-medium truncate ${selectedFile === file ? 'text-indigo-700 dark:text-indigo-300' : 'text-gray-600 dark:text-gray-300 group-hover:text-gray-900 dark:group-hover:text-white'}`}>
                                            {file}
                                        </span>
                                    </div>
                                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${sevColor}`}>
                                        {issueCount}
                                    </span>
                                </div>
                            </button>
                        );
                    }))}
            </div>
        </div>
    );
};

export default FileListSidebar;
