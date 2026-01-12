<?php

namespace App\Services;

use App\Services\ComplexityAnalyzer;
use App\Services\SecurityAnalyzer;
use App\Services\StyleAnalyzer;

/**
 * Main Code Analysis Service
 * Orchestrates all analysis services and aggregates results
 */
class CodeAnalysisService
{

    private ComplexityAnalyzer $complexityAnalyzer;
    private SecurityAnalyzer $securityAnalyzer;
    private StyleAnalyzer $styleAnalyzer;

    public function __construct()
    {
        $this->complexityAnalyzer = new ComplexityAnalyzer();
        $this->securityAnalyzer = new SecurityAnalyzer();
        $this->styleAnalyzer = new StyleAnalyzer();
    }

    /**
     * Analyze code and return comprehensive analysis results
     * 
     * @param string $code The code to analyze
     * @param string $filePath Optional file path for reporting
     * @return array Complete analysis results
     */
    public function analyzeCode(string $code, string $filePath = 'unknown'): array
    {
        // Run all analyzers
        $complexityResult = $this->complexityAnalyzer->analyze($code, $filePath);
        $securityIssues = $this->securityAnalyzer->analyze($code, $filePath);
        $styleIssues = $this->styleAnalyzer->analyze($code, $filePath);

        // Combine all issues
        $rawIssues = array_merge(
            $complexityResult['issues'],
            $securityIssues,
            $styleIssues
        );

        // Normalize issues keys to camelCase
        $allIssues = array_map(function ($issue) {
            return [
                'filePath' => $issue['file_path'] ?? $issue['filePath'] ?? '',
                'lineNumber' => $issue['line_number'] ?? $issue['lineNumber'] ?? 0,
                'severity' => $issue['severity'] ?? 'info',
                'category' => $issue['category'] ?? 'other',
                'message' => $issue['message'] ?? '',
                'ruleId' => $issue['rule_id'] ?? $issue['ruleId'] ?? '',
                'suggestedFix' => $issue['suggested_fix'] ?? $issue['suggestedFix'] ?? ''
            ];
        }, $rawIssues);

        // Calculate overall score
        $score = $this->calculateScore($allIssues, $complexityResult['metrics']);

        // Group issues by severity
        $issuesBySeverity = $this->groupIssuesBySeverity($allIssues);

        return [
            'success' => true,
            'filePath' => $filePath,
            'metrics' => $complexityResult['metrics'],
            'score' => $score,
            'issues' => $allIssues,
            'issuesBySeverity' => $issuesBySeverity,
            'summary' => [
                'totalIssues' => count($allIssues),
                'criticalCount' => count($issuesBySeverity['critical'] ?? []),
                'majorCount' => count($issuesBySeverity['major'] ?? []),
                'minorCount' => count($issuesBySeverity['minor'] ?? []),
                'infoCount' => count($issuesBySeverity['info'] ?? []),
            ]
        ];
    }

    /**
     * Analyze multiple files
     * 
     * @param array $files Array of ['path' => string, 'content' => string]
     * @return array Analysis results for all files
     */
    public function analyzeMultipleFiles(array $files): array
    {
        $results = [];
        $totalIssues = 0;
        $totalScore = 0;

        foreach ($files as $file) {
            $filePath = $file['path'] ?? 'unknown';
            $content = $file['content'] ?? '';

            $result = $this->analyzeCode($content, $filePath);
            $results[] = $result;

            $totalIssues += $result['summary']['totalIssues']; // Updated key
            $totalScore += $result['score'];
        }

        $averageScore = count($files) > 0 ? $totalScore / count($files) : 0;

        return [
            'success' => true,
            'filesAnalyzed' => count($files),
            'totalIssues' => $totalIssues,
            'averageScore' => round($averageScore, 2),
            'results' => $results
        ];
    }

    /**
     * Calculate overall code quality score (0-100)
     * Higher is better
     */
    private function calculateScore(array $issues, array $metrics): int
    {
        $score = 100;

        // Deduct points for issues by severity
        foreach ($issues as $issue) {
            switch ($issue['severity']) {
                case 'critical':
                    $score -= 10;
                    break;
                case 'major':
                    $score -= 5;
                    break;
                case 'minor':
                    $score -= 2;
                    break;
                case 'info':
                    $score -= 1;
                    break;
            }
        }

        // Deduct points for high complexity
        $complexityLevel = $metrics['complexity_level'] ?? 'low';
        switch ($complexityLevel) {
            case 'very_high':
                $score -= 20;
                break;
            case 'high':
                $score -= 10;
                break;
            case 'medium':
                $score -= 5;
                break;
        }

        // Ensure score is between 0 and 100
        return max(0, min(100, $score));
    }

    /**
     * Group issues by severity
     */
    private function groupIssuesBySeverity(array $issues): array
    {
        $grouped = [
            'critical' => [],
            'major' => [],
            'minor' => [],
            'info' => []
        ];

        foreach ($issues as $issue) {
            $severity = $issue['severity'] ?? 'info';
            if (isset($grouped[$severity])) {
                $grouped[$severity][] = $issue;
            }
        }

        return $grouped;
    }
}

?>