import { render, screen, fireEvent } from '@testing-library/react';
import AnalysisView from '../AnalysisView';

// Mock child components
jest.mock('../AnalysisHeroCard', () => () => <div data-testid="hero-card">Hero Card</div>);
jest.mock('../FilterBar', () => ({ severityFilter, setSeverityFilter, searchQuery, setSearchQuery }) => (
    <div data-testid="filter-bar">
        <input
            data-testid="search-input"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
        />
        <button onClick={() => setSeverityFilter('Critical')}>Filter Critical</button>
    </div>
));
jest.mock('../FileTree', () => ({ filteredIssues }) => (
    <div data-testid="file-tree">
        {filteredIssues.map((issue, idx) => (
            <div key={idx} data-testid="file-issue">{issue.message}</div>
        ))}
    </div>
));
jest.mock('../CodeInspector', () => () => <div data-testid="code-inspector">Code Inspector</div>);

describe('AnalysisView Component', () => {
    const mockResult = {
        issues: [
            { file: 'test.php', severity: 'High', message: 'High Issue', category: 'Security' },
            { file: 'app.js', severity: 'Critical', message: 'Critical Issue', category: 'Bug' }
        ],
        fileContents: {},
        fileMetrics: {}
    };

    test('renders all main sections', () => {
        render(<AnalysisView result={mockResult} score={85} repoUrl="http://github.com/test" branchName="main" extractRepoName={() => 'test'} />);

        expect(screen.getByTestId('hero-card')).toBeInTheDocument();
        expect(screen.getByTestId('filter-bar')).toBeInTheDocument();
        expect(screen.getByTestId('file-tree')).toBeInTheDocument();
        expect(screen.getByTestId('code-inspector')).toBeInTheDocument();
    });

    test('filters issues by severity', () => {
        render(<AnalysisView result={mockResult} />);

        // Initially should show all
        expect(screen.getAllByTestId('file-issue')).toHaveLength(2);

        // Click filter button (mocked above)
        fireEvent.click(screen.getByText('Filter Critical'));

        // Should now only show Critical issue
        const fileIssues = screen.getAllByTestId('file-issue');
        expect(fileIssues).toHaveLength(1);
        expect(fileIssues[0]).toHaveTextContent('Critical Issue');
    });

    test('filters issues by search query', () => {
        render(<AnalysisView result={mockResult} />);

        const searchInput = screen.getByTestId('search-input');
        fireEvent.change(searchInput, { target: { value: 'High' } });

        const fileIssues = screen.getAllByTestId('file-issue');
        expect(fileIssues).toHaveLength(1);
        expect(fileIssues[0]).toHaveTextContent('High Issue');
    });

    test('handles empty result gracefully', () => {
        render(<AnalysisView result={null} />);
        expect(screen.getByTestId('hero-card')).toBeInTheDocument();
        // Should not crash
    });
});
