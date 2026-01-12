<?php
/**
 * Lightweight functional tests for PHP analyzers
 */

require_once __DIR__ . '/../vendor/autoload.php';

use App\Services\StyleAnalyzer;
use App\Services\ComplexityAnalyzer;
use App\Services\SecurityAnalyzer;

function runTest($name, $callback)
{
    echo "Running test: $name... ";
    try {
        $callback();
        echo "PASS\n";
    } catch (Exception $e) {
        echo "FAIL: " . $e->getMessage() . "\n";
        exit(1);
    }
}

// 1. StyleAnalyzer Tests
runTest("StyleAnalyzer - Detects snake_case functions", function () {
    $analyzer = new StyleAnalyzer();
    $code = "<?php function my_function_name() { }";
    $issues = $analyzer->analyze($code, 'test.php');

    foreach ($issues as $issue) {
        if (strpos($issue['message'], 'uses snake_case') !== false)
            return;
    }
    throw new Exception("Did not detect snake_case function");
});

runTest("StyleAnalyzer - Detects long lines", function () {
    $analyzer = new StyleAnalyzer();
    $code = "<?php\n" . str_repeat("a", 130);
    $issues = $analyzer->analyze($code, 'test.php');

    foreach ($issues as $issue) {
        if (strpos($issue['message'], 'Line exceeds 120') !== false)
            return;
    }
    throw new Exception("Did not detect long line");
});

runTest("StyleAnalyzer - Identifies deep nesting", function () {
    $analyzer = new StyleAnalyzer();
    $code = "<?php
    if (true) {
        if (true) {
            if (true) {
                if (true) {
                    if (true) {
                        return true;
                    }
                }
            }
        }
    }";
    $issues = $analyzer->analyze($code, 'test.php');

    foreach ($issues as $issue) {
        if (strpos($issue['message'], 'nesting') !== false)
            return;
    }
    throw new Exception("Did not detect deep nesting");
});

// 2. ComplexityAnalyzer Tests
runTest("ComplexityAnalyzer - Calculates Cyclomatic Complexity", function () {
    $analyzer = new ComplexityAnalyzer();
    $code = "<?php
    if (true) {
        if (true) {
            return true;
        }
    }";
    $result = $analyzer->analyze($code, 'test.php');

    if ($result['metrics']['cyclomatic_complexity'] < 3) {
        throw new Exception("Complexity calculation wrong, expected at least 3, got " . $result['metrics']['cyclomatic_complexity']);
    }
});

// 3. SecurityAnalyzer Tests
runTest("SecurityAnalyzer - Detects potential SQL injection", function () {
    $analyzer = new SecurityAnalyzer();
    $code = "<?php \$query = \"SELECT * FROM users WHERE id = \$id\";";
    $issues = $analyzer->analyze($code, 'test.php');

    foreach ($issues as $issue) {
        if (strpos($issue['message'], 'SQL Injection') !== false)
            return;
    }
    throw new Exception("Did not detect potential SQL injection");
});

echo "\nSummary: All PHP tests passed successfully!\n";
