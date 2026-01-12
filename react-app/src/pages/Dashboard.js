import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import { useAnalysisRuns } from '../hooks/useAnalysis';
import { Sidebar, AnalysisForm, HistoryView, ResultsView } from '../components/dashboard';

const Dashboard = () => {
    const { logout } = useAuth();
    const navigate = useNavigate();
    const { refetch: refetchRuns } = useAnalysisRuns();

    // View state
    const [view, setView] = useState('analyze'); // 'analyze' | 'history' | 'results'
    const [selectedAnalysisId, setSelectedAnalysisId] = useState(null);
    const [currentRunId, setCurrentRunId] = useState(null);
    const [repoUrl, setRepoUrl] = useState('');
    const [branchName, setBranchName] = useState('');

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    const handleAnalysisStart = (runId, repo, branch) => {
        // Update repo URL and branch name from the form
        if (repo) setRepoUrl(repo);
        if (branch) setBranchName(branch);

        if (runId) {
            setCurrentRunId(runId);
            setView('results');
            refetchRuns();
        }
    };

    const handleSelectAnalysis = (analysisId, repo, branch) => {
        setSelectedAnalysisId(analysisId);
        setCurrentRunId(null);
        // Update repo info for display in results view
        if (repo) setRepoUrl(repo);
        if (branch) setBranchName(branch);
        setView('results');
    };

    const handleStartNewAnalysis = () => {
        setView('analyze');
        setSelectedAnalysisId(null);
        setCurrentRunId(null);
    };

    const activeRunId = selectedAnalysisId || currentRunId;

    return (
        <div className="min-h-screen bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white transition-colors duration-200 font-sans">
            <Sidebar
                view={view}
                setView={setView}
                onLogout={handleLogout}
            />

            <main className="md:ml-64 p-4 md:p-8 max-w-7xl mx-auto space-y-6">
                {view === 'analyze' && (
                    <AnalysisForm onAnalysisStart={handleAnalysisStart} />
                )}

                {view === 'history' && (
                    <HistoryView onSelectAnalysis={handleSelectAnalysis} />
                )}

                {view === 'results' && (
                    <ResultsView
                        runId={activeRunId}
                        repoUrl={repoUrl}
                        branchName={branchName}
                        onStartNewAnalysis={handleStartNewAnalysis}
                    />
                )}
            </main>
        </div>
    );
};

export default Dashboard;
