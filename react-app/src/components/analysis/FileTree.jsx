import React, { useState, useMemo } from 'react';
import { FolderTree, Maximize2, Minimize2, FileText } from 'lucide-react';
import { buildFileTree } from '../../utils/treeUtils';
import TreeNode from './TreeNode';

/**
 * IDE-style file tree navigator with expansion controls and severity bubbling.
 * Replaces the flat FileListSidebar with a hierarchical folder/file structure.
 */
const FileTree = ({ uniqueFiles, filteredIssues, selectedFile, onSelectFile }) => {
    const [expandedFolders, setExpandedFolders] = useState({});

    // 🚀 Performance: Only rebuild tree when files/issues change
    const tree = useMemo(() => {
        if (!uniqueFiles || uniqueFiles.length === 0) return null;
        return buildFileTree(uniqueFiles, filteredIssues);
    }, [uniqueFiles, filteredIssues]);

    // Toggle a single folder's expansion state
    const toggleFolder = (path) => {
        setExpandedFolders((prev) => ({
            ...prev,
            [path]: !prev[path]
        }));
    };

    // Expand all folders recursively
    const expandAll = () => {
        const allFolders = {};
        const traverse = (node) => {
            if (node.type === 'folder') {
                allFolders[node.path] = true;
                if (node.children) {
                    node.children.forEach(traverse);
                }
            }
        };
        if (tree) traverse(tree);
        setExpandedFolders(allFolders);
    };

    // Collapse all folders
    const collapseAll = () => {
        setExpandedFolders({});
    };

    if (!tree || !tree.children || tree.children.length === 0) {
        return (
            <div className="lg:col-span-1 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm transition-colors">
                <div className="p-4 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center bg-gray-50/50 dark:bg-gray-900/50 rounded-t-xl">
                    <h4 className="font-semibold text-gray-900 dark:text-white flex items-center gap-2 text-sm uppercase tracking-wide">
                        <FolderTree className="w-4 h-4 text-gray-500" />
                        File Explorer
                    </h4>
                </div>
                <div className="p-8 text-center text-gray-500 dark:text-gray-400">
                    <FileText className="w-12 h-12 mx-auto mb-3 opacity-50" />
                    <p className="text-sm">No files found</p>
                </div>
            </div>
        );
    }

    const totalFiles = uniqueFiles.length;
    const totalIssues = filteredIssues.length;

    return (
        <div className="lg:col-span-1 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 flex flex-col max-h-[calc(100vh-250px)] sticky top-40 shadow-sm transition-colors">
            {/* Header with Stats */}
            <div className="p-3 border-b border-gray-100 dark:border-gray-700 bg-gray-50/80 dark:bg-gray-900/80 rounded-t-xl">
                {/* Expansion Controls */}
                <div className="flex gap-2">
                    <button
                        onClick={expandAll}
                        className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg hover:border-indigo-400 hover:text-indigo-600 transition-all shadow-sm"
                        title="Expand all"
                    >
                        <Maximize2 className="w-3 h-3" />
                        Expand
                    </button>
                    <button
                        onClick={collapseAll}
                        className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg hover:border-indigo-400 hover:text-indigo-600 transition-all shadow-sm"
                        title="Collapse all"
                    >
                        <Minimize2 className="w-3 h-3" />
                        Collapse
                    </button>
                </div>
            </div>

            {/* Tree View */}
            <div className="flex-1 overflow-y-auto p-2 space-y-0.5 custom-scrollbar">
                {tree.children.map((node) => (
                    <TreeNode
                        key={node.path}
                        node={node}
                        level={0}
                        selectedFile={selectedFile}
                        onSelectFile={onSelectFile}
                        expandedFolders={expandedFolders}
                        toggleFolder={toggleFolder}
                    />
                ))}
            </div>

            {/* Legend (Optional Footer) */}
            <div className="p-3 border-t border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-900/50 rounded-b-xl">
                <div className="flex items-center gap-4 text-xs text-gray-600 dark:text-gray-400">
                    <div className="flex items-center gap-1.5">
                        <span className="w-2 h-2 rounded-full bg-red-500" />
                        <span>Critical</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                        <span className="w-2 h-2 rounded-full bg-orange-500" />
                        <span>Major</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                        <span className="w-2 h-2 rounded-full bg-yellow-500" />
                        <span>Minor</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                        <span className="w-2 h-2 rounded-full bg-blue-500" />
                        <span>Info</span>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default FileTree;
