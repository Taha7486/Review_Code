/**
 * Utility to transform a flat list of file paths into a nested tree structure.
 * Each node includes metadata for issue counts and maximum severity bubbling.
 */

const SEVERITY_RANK = {
    'critical': 4,
    'major': 3,
    'minor': 2,
    'info': 1,
    'none': 0
};

/**
 * Builds a nested tree structure from flat file paths and their associated issues.
 * @param {string[]} files List of all unique file paths in the analysis.
 * @param {any[]} issues List of filtered issues to calculate counts and severities.
 * @returns {object} The root of the nested file tree.
 */
export const buildFileTree = (files, issues) => {
    const root = {
        name: 'root',
        type: 'folder',
        path: '',
        children: {},
        issueCount: 0,
        maxSeverity: 'none'
    };

    // 1. Create the base tree structure
    files.forEach(filePath => {
        const parts = filePath.split('/');
        let current = root;

        parts.forEach((part, index) => {
            const isFile = index === parts.length - 1;
            const currentPath = parts.slice(0, index + 1).join('/');

            if (!current.children[part]) {
                current.children[part] = {
                    name: part,
                    path: currentPath,
                    type: isFile ? 'file' : 'folder',
                    children: isFile ? null : {},
                    issueCount: 0,
                    maxSeverity: 'none'
                };
            }
            current = current.children[part];
        });
    });

    // 2. Map issues to files to calculate initial counts/severities
    const fileIssueMap = {};
    issues.forEach(issue => {
        const path = (issue.filePath || issue.FilePath || issue.file || issue.File || '').toLowerCase();
        const severity = (issue.severity || issue.Severity || 'info').toLowerCase();

        if (!fileIssueMap[path]) {
            fileIssueMap[path] = { count: 0, maxSev: 'none' };
        }

        fileIssueMap[path].count++;
        if (SEVERITY_RANK[severity] > SEVERITY_RANK[fileIssueMap[path].maxSev]) {
            fileIssueMap[path].maxSev = severity;
        }
    });

    // 3. Convert children objects to sorted arrays and bubble up metrics
    const finalizeNode = (node) => {
        if (node.type === 'file') {
            const metrics = fileIssueMap[node.path.toLowerCase()] || { count: 0, maxSev: 'none' };
            node.issueCount = metrics.count;
            node.maxSeverity = metrics.maxSev;
            return { count: node.issueCount, maxSev: node.maxSeverity };
        }

        // It's a folder
        const childrenArray = Object.values(node.children);
        let totalCount = 0;
        let nodeMaxSev = 'none';

        childrenArray.forEach(child => {
            const { count, maxSev } = finalizeNode(child);
            totalCount += count;
            if (SEVERITY_RANK[maxSev] > SEVERITY_RANK[nodeMaxSev]) {
                nodeMaxSev = maxSev;
            }
        });

        // Sort: Folders first, then alphabetically
        node.children = childrenArray.sort((a, b) => {
            if (a.type !== b.type) return a.type === 'folder' ? -1 : 1;
            return a.name.localeCompare(b.name);
        });

        node.issueCount = totalCount;
        node.maxSeverity = nodeMaxSev;
        return { count: totalCount, maxSev: nodeMaxSev };
    };

    finalizeNode(root);
    return root;
};
