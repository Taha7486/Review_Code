import React from 'react';
import { ChevronRight, ChevronDown, Folder, FolderOpen } from 'lucide-react';
import FileIcon from './FileIcon';

/**
 * Recursive tree node component that renders both folders and files
 * with severity indicators and expansion controls.
 */
const TreeNode = ({
    node,
    level = 0,
    selectedFile,
    onSelectFile,
    expandedFolders,
    toggleFolder
}) => {
    const isFolder = node.type === 'folder';
    const isExpanded = expandedFolders[node.path];
    const isSelected = !isFolder && selectedFile === node.path;

    // Severity color mapping
    const severityColors = {
        critical: 'text-red-600 bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800',
        major: 'text-orange-600 bg-orange-50 dark:bg-orange-900/20 border-orange-200 dark:border-orange-800',
        minor: 'text-yellow-600 bg-yellow-50 dark:bg-yellow-900/20 border-yellow-200 dark:border-yellow-800',
        info: 'text-blue-600 bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800',
        none: 'text-gray-500 bg-gray-50 dark:bg-gray-900/20 border-gray-200 dark:border-gray-700'
    };

    // Severity indicator dot colors
    const severityDotColors = {
        critical: 'bg-red-500',
        major: 'bg-orange-500',
        minor: 'bg-yellow-500',
        info: 'bg-blue-500',
        none: 'bg-gray-400'
    };

    const severityColor = severityColors[node.maxSeverity] || severityColors.none;
    const dotColor = severityDotColors[node.maxSeverity] || severityDotColors.none;

    const handleClick = () => {
        if (isFolder) {
            toggleFolder(node.path);
        } else {
            onSelectFile(node.path);
        }
    };

    return (
        <div>
            {/* Current Node */}
            <button
                onClick={handleClick}
                className={`w-full text-left px-3 py-2 rounded-lg transition-all border group flex items-center gap-2 ${isSelected
                        ? 'bg-indigo-50 dark:bg-indigo-900/30 border-indigo-200 dark:border-indigo-800 shadow-sm'
                        : 'border-transparent hover:bg-gray-50 dark:hover:bg-gray-700/50'
                    }`}
                style={{ paddingLeft: `${level * 16 + 12}px` }}
            >
                {/* Expansion Icon (Folders Only) */}
                {isFolder && (
                    <span className="flex-shrink-0">
                        {isExpanded ? (
                            <ChevronDown className="w-4 h-4 text-gray-500" />
                        ) : (
                            <ChevronRight className="w-4 h-4 text-gray-500" />
                        )}
                    </span>
                )}

                {/* Folder/File Icon */}
                <span className="flex-shrink-0">
                    {isFolder ? (
                        isExpanded ? (
                            <FolderOpen className="w-4 h-4 text-blue-500" />
                        ) : (
                            <Folder className="w-4 h-4 text-blue-400" />
                        )
                    ) : (
                        <FileIcon filename={node.name} />
                    )}
                </span>

                {/* Node Name */}
                <span
                    className={`text-sm font-medium truncate flex-1 ${isSelected
                            ? 'text-indigo-700 dark:text-indigo-300'
                            : 'text-gray-700 dark:text-gray-300 group-hover:text-gray-900 dark:group-hover:text-white'
                        }`}
                    title={node.name}
                >
                    {node.name}
                </span>

                {/* Severity Indicator + Issue Count */}
                {node.issueCount > 0 && (
                    <div className="flex items-center gap-2">
                        {/* Severity Dot */}
                        <span
                            className={`w-2 h-2 rounded-full ${dotColor} ${node.maxSeverity === 'critical' ? 'animate-pulse' : ''
                                }`}
                            title={`Max severity: ${node.maxSeverity}`}
                        />
                        {/* Issue Count Badge */}
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium border ${severityColor}`}>
                            {node.issueCount}
                        </span>
                    </div>
                )}
            </button>

            {/* Render Children (Recursive) */}
            {isFolder && isExpanded && node.children && (
                <div>
                    {node.children.map((child) => (
                        <TreeNode
                            key={child.path}
                            node={child}
                            level={level + 1}
                            selectedFile={selectedFile}
                            onSelectFile={onSelectFile}
                            expandedFolders={expandedFolders}
                            toggleFolder={toggleFolder}
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

export default TreeNode;
