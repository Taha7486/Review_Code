<?php
namespace App\Controllers;

use Psr\Http\Message\ResponseInterface as Response;
use Psr\Http\Message\ServerRequestInterface as Request;

class MetricsController
{
    // Persistent storage file path
    private const METRICS_FILE = '/tmp/php_metrics.json';

    // In-memory cache (loaded from file)
    private static $metricsCache = null;

    /**
     * Load metrics from persistent storage
     */
    private static function loadMetrics(): array
    {
        if (self::$metricsCache !== null) {
            return self::$metricsCache;
        }

        if (file_exists(self::METRICS_FILE)) {
            $data = @file_get_contents(self::METRICS_FILE);
            $metrics = json_decode($data, true);
            if ($metrics) {
                self::$metricsCache = $metrics;
                return $metrics;
            }
        }

        // Default metrics structure
        self::$metricsCache = [
            'analysesProcessed' => 0,
            'filesAnalyzed' => 0,
            'issuesCounts' => [
                'critical' => 0,
                'high' => 0,
                'medium' => 0,
                'low' => 0
            ],
            'analysisTimes' => []
        ];

        return self::$metricsCache;
    }

    /**
     * Save metrics to persistent storage
     */
    private static function saveMetrics(): void
    {
        if (self::$metricsCache !== null) {
            @file_put_contents(self::METRICS_FILE, json_encode(self::$metricsCache));
        }
    }

    /**
     * Expose Prometheus metrics in text format
     */
    public function metrics(Request $request, Response $response): Response
    {
        $metrics = $this->generatePrometheusFormat();
        $response->getBody()->write($metrics);
        return $response->withHeader('Content-Type', 'text/plain; version=0.0.4');
    }

    /**
     * Generate Prometheus text format
     */
    private function generatePrometheusFormat(): string
    {
        $metrics = self::loadMetrics();
        $output = "";

        // 1. Analyses processed counter
        $output .= "# HELP php_analyses_processed_total Total analyses processed by PHP service\n";
        $output .= "# TYPE php_analyses_processed_total counter\n";
        $output .= "php_analyses_processed_total " . $metrics['analysesProcessed'] . "\n\n";

        // 2. Files analyzed counter
        $output .= "# HELP php_files_analyzed_total Total files analyzed\n";
        $output .= "# TYPE php_files_analyzed_total counter\n";
        $output .= "php_files_analyzed_total " . $metrics['filesAnalyzed'] . "\n\n";

        // 3. Issues detected by severity
        $output .= "# HELP php_issues_detected_total Issues detected by severity\n";
        $output .= "# TYPE php_issues_detected_total counter\n";
        foreach ($metrics['issuesCounts'] as $severity => $count) {
            $output .= "php_issues_detected_total{severity=\"$severity\"} $count\n";
        }
        $output .= "\n";

        // 4. Memory usage gauge
        $output .= "# HELP php_memory_usage_bytes Current memory usage\n";
        $output .= "# TYPE php_memory_usage_bytes gauge\n";
        $output .= "php_memory_usage_bytes " . memory_get_usage(true) . "\n\n";

        // 5. Memory peak gauge
        $output .= "# HELP php_memory_peak_bytes Peak memory usage\n";
        $output .= "# TYPE php_memory_peak_bytes gauge\n";
        $output .= "php_memory_peak_bytes " . memory_get_peak_usage(true) . "\n\n";

        // 6. Average analysis time (if we have data)
        if (!empty($metrics['analysisTimes'])) {
            $avgTime = array_sum($metrics['analysisTimes']) / count($metrics['analysisTimes']);
            $output .= "# HELP php_analysis_duration_seconds_avg Average analysis duration\n";
            $output .= "# TYPE php_analysis_duration_seconds_avg gauge\n";
            $output .= "php_analysis_duration_seconds_avg " . number_format($avgTime, 3) . "\n\n";
        }

        // 7. Process info
        $output .= "# HELP php_info PHP version and service info\n";
        $output .= "# TYPE php_info gauge\n";
        $output .= "php_info{version=\"" . PHP_VERSION . "\",service=\"analysis-engine\"} 1\n\n";

        return $output;
    }

    /**
     * Increment analysis counter (call this from AnalyzeController)
     */
    public static function incrementAnalysisProcessed(): void
    {
        $metrics = self::loadMetrics();
        $metrics['analysesProcessed']++;
        self::$metricsCache = $metrics;
        self::saveMetrics();
    }

    /**
     * Increment files analyzed
     */
    public static function incrementFilesAnalyzed(int $count = 1): void
    {
        $metrics = self::loadMetrics();
        $metrics['filesAnalyzed'] += $count;
        self::$metricsCache = $metrics;
        self::saveMetrics();
    }

    /**
     * Track issue by severity
     */
    public static function trackIssue(string $severity): void
    {
        $metrics = self::loadMetrics();
        $severity = strtolower($severity);
        if (isset($metrics['issuesCounts'][$severity])) {
            $metrics['issuesCounts'][$severity]++;
            self::$metricsCache = $metrics;
            self::saveMetrics();
        }
    }

    /**
     * Record analysis duration
     */
    public static function recordAnalysisTime(float $durationSeconds): void
    {
        $metrics = self::loadMetrics();
        $metrics['analysisTimes'][] = $durationSeconds;

        // Keep only last 100 measurements to avoid memory bloat
        if (count($metrics['analysisTimes']) > 100) {
            array_shift($metrics['analysisTimes']);
        }

        self::$metricsCache = $metrics;
        self::saveMetrics();
    }
}
