import React, { useState, useRef } from 'react';
import AnalysisHeroCard from './AnalysisHeroCard';
import FilterBar from './FilterBar';
import FileListSidebar from './FileListSidebar';
import CodeInspector from './CodeInspector';

const AnalysisView = ({ result, repoUrl, branchName, score, extractRepoName }) => {
    const [severityFilter, setSeverityFilter] = useState('all');
    const [categoryFilter, setCategoryFilter] = useState('all');
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedFile, setSelectedFile] = useState(null);
    const [currentIssueIndex, setCurrentIssueIndex] = useState(0);
    const issueRefs = useRef({});

    // Filter computation
    const filteredIssues = result ? (result.issues || []).filter(issue => {
        const file = (issue.filePath || issue.FilePath || issue.file || issue.File || '').toLowerCase();
        const severity = (issue.severity || issue.Severity || '').toLowerCase();
        const category = (issue.category || issue.Category || '').toLowerCase();
        const message = (issue.message || issue.Message || '').toLowerCase();
        const ruleId = (issue.ruleId || issue.RuleId || issue.rule || issue.Rule || '').toLowerCase();

        const matchesSeverity = severityFilter === 'all' || severity === severityFilter;
        const matchesCategory = categoryFilter === 'all' || category === categoryFilter.toLowerCase();
        const matchesSearch = !searchQuery ||
            file.includes(searchQuery.toLowerCase()) ||
            message.includes(searchQuery.toLowerCase()) ||
            ruleId.includes(searchQuery.toLowerCase());

        return matchesSeverity && matchesCategory && matchesSearch;
    }) : [];

    const fileIssues = selectedFile ? filteredIssues.filter(i => {
        const issueFile = (i.file || i.File || '').toLowerCase();
        return issueFile === selectedFile.toLowerCase();
    }) : [];

    // Get unique files from multiple sources (issues, fileContents, fileMetrics)
    // Handle both camelCase and PascalCase property names
    const filesFromIssues = result ? (result.issues || []).map(i => i.filePath || i.FilePath || i.file || i.File).filter(Boolean) : [];
    const filesFromContents = result ? Object.keys(result.fileContents || {}) : [];
    const filesFromMetrics = result ? Object.keys(result.fileMetrics || {}) : [];
    const uniqueFiles = result ? [...new Set([...filesFromIssues, ...filesFromContents, ...filesFromMetrics])] : [];

    const uniqueCategories = result ? [...new Set((result.issues || []).map(i => i.category || i.Category).filter(Boolean))] : [];
    const fileContent = result && selectedFile ? (result.fileContents || {})[selectedFile] : '';

    const handleClearFilters = () => {
        setSeverityFilter('all');
        setCategoryFilter('all');
        setSearchQuery('');
        setSelectedFile(null);
    };

    const handleSelectFile = (file) => {
        setSelectedFile(file);
        setCurrentIssueIndex(0);
        const firstIssue = filteredIssues.find(i => {
            const issueFile = (i.file || i.File || '').toLowerCase();
            return issueFile === file.toLowerCase();
        });
        if (firstIssue) {
            setTimeout(() => {
                const issueFile = (firstIssue.file || firstIssue.File || file).toLowerCase();
                const issueLine = firstIssue.line || firstIssue.Line || 0;
                const ref = issueRefs.current[`${issueFile}-${issueLine}`];
                if (ref) ref.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }, 100);
        }
    };

    const handleJumpToIssue = (direction) => {
        if (!selectedFile || !fileIssues.length) return;

        let newIndex;
        if (direction === 'next') {
            newIndex = currentIssueIndex + 1 >= fileIssues.length ? 0 : currentIssueIndex + 1;
        } else {
            newIndex = currentIssueIndex - 1 < 0 ? fileIssues.length - 1 : currentIssueIndex - 1;
        }

        setCurrentIssueIndex(newIndex);

        const issue = fileIssues[newIndex];
        if (issue) {
            const file = (issue.file || issue.File || selectedFile).toLowerCase();
            const line = issue.line || issue.Line || 0;
            const ref = issueRefs.current[`${file}-${line}`];
            if (ref) {
                ref.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    };

    return (
        <div className="space-y-6">
            {/* Hero Card */}
            <AnalysisHeroCard
                result={result}
                score={score}
                filteredIssues={filteredIssues}
                extractRepoName={extractRepoName}
                repoUrl={repoUrl}
                branchName={branchName}
            />

            {/* Filters Bar */}
            <FilterBar
                severityFilter={severityFilter}
                setSeverityFilter={setSeverityFilter}
                categoryFilter={categoryFilter}
                setCategoryFilter={setCategoryFilter}
                searchQuery={searchQuery}
                setSearchQuery={setSearchQuery}
                uniqueCategories={uniqueCategories}
                onClearFilters={handleClearFilters}
            />

            {/* File List + Code Inspector Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-4 gap-6 items-start">
                <FileListSidebar
                    uniqueFiles={uniqueFiles}
                    filteredIssues={filteredIssues}
                    selectedFile={selectedFile}
                    onSelectFile={handleSelectFile}
                />

                <div className="lg:col-span-3">
                    <CodeInspector
                        selectedFile={selectedFile}
                        fileContent={fileContent}
                        fileIssues={fileIssues}
                        currentIssueIndex={currentIssueIndex}
                        onJumpToIssue={handleJumpToIssue}
                        issueRefs={issueRefs}
                    />
                </div>
            </div>
        </div>
    );
};

export default AnalysisView;
