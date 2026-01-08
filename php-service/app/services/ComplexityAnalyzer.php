<?php

namespace App\Services;

/**
 * Complexity Analysis Service
 * Analyzes code for cyclomatic complexity and other complexity metrics
 */
class ComplexityAnalyzer
{

    private array $issues = [];

    /**
     * Analyze code for complexity issues
     * 
     * @param string $code The code to analyze
     * @param string $filePath The file path (for reporting)
     * @return array Array containing metrics and issues
     */
    public function analyze(string $code, string $filePath = 'unknown'): array
    {
        $this->issues = [];

        $cyclomaticComplexity = $this->calculateCyclomaticComplexity($code);
        $codeLength = strlen($code);
        $linesOfCode = substr_count($code, "\n") + 1;

        // Count various code elements
        $functionCount = preg_match_all('/\bfunction\s+\w+\s*\(/i', $code);
        $classCount = preg_match_all('/\bclass\s+\w+/i', $code);

        // Check for complexity issues
        $this->checkFunctionLengths($code, $filePath);
        $this->checkFileLengths($code, $filePath, $linesOfCode);
        $this->checkHighComplexity($code, $filePath, $cyclomaticComplexity);

        return [
            'metrics' => [
                'cyclomatic_complexity' => $cyclomaticComplexity,
                'code_length' => $codeLength,
                'lines_of_code' => $linesOfCode,
                'function_count' => $functionCount,
                'class_count' => $classCount,
                'complexity_level' => $this->getComplexityLevel($cyclomaticComplexity)
            ],
            'issues' => $this->issues
        ];
    }

    public function calculateCyclomaticComplexity(string $code): int
    {
        // Base complexity is 1
        $complexity = 1;

        // Tokenize the code
        $tokens = token_get_all($code);
        $tokenCount = count($tokens);

        for ($i = 0; $i < $tokenCount; $i++) {
            $token = $tokens[$i];

            // Skip if token is not an array
            if (!is_array($token)) {
                continue;
            }

            $tokenType = $token[0];
            $tokenValue = $token[1];

            // Count decision points
            switch ($tokenType) {
                case T_IF:
                case T_ELSEIF:
                case T_ELSE:
                case T_SWITCH:
                case T_CASE:
                case T_WHILE:
                case T_FOR:
                case T_FOREACH:
                case T_CATCH:
                    $complexity++;
                    break;

                case T_BOOLEAN_AND:  // &&
                case T_BOOLEAN_OR:   // ||
                    $complexity++;
                    break;
            }
        }

        return $complexity;
    }

    /**
     * Check for functions that are too long
     */
    private function checkFunctionLengths(string $code, string $filePath): void
    {
        $maxLines = 50;

        // Find all functions
        if (preg_match_all('/function\s+(\w+)\s*\([^)]*\)\s*\{/i', $code, $matches, PREG_OFFSET_CAPTURE)) {
            foreach ($matches[0] as $index => $match) {
                $functionName = $matches[1][$index][0];
                $startOffset = $match[1];
                $startLine = $this->getLineNumber($code, $startOffset);

                // Find the closing brace
                $braceCount = 1;
                $currentPos = $startOffset + strlen($match[0]);
                $endLine = $startLine;

                while ($braceCount > 0 && $currentPos < strlen($code)) {
                    if ($code[$currentPos] === '{')
                        $braceCount++;
                    if ($code[$currentPos] === '}')
                        $braceCount--;
                    if ($code[$currentPos] === "\n")
                        $endLine++;
                    $currentPos++;
                }

                $functionLength = $endLine - $startLine;

                if ($functionLength > $maxLines) {
                    $this->addIssue(
                        $filePath,
                        $startLine,
                        'major',
                        'complexity',
                        "Function '$functionName' is too long ($functionLength lines). Consider breaking it into smaller functions.",
                        'FUNCTION_TOO_LONG',
                        "Refactor into smaller, focused functions (recommended max: $maxLines lines)"
                    );
                }
            }
        }
    }

    /**
     * Check if file is too long
     */
    private function checkFileLengths(string $code, string $filePath, int $linesOfCode): void
    {
        $maxLines = 500;

        if ($linesOfCode > $maxLines) {
            $this->addIssue(
                $filePath,
                1,
                'major',
                'complexity',
                "File is too long ($linesOfCode lines). Consider splitting into multiple files.",
                'FILE_TOO_LONG',
                "Break into smaller, focused files (recommended max: $maxLines lines)"
            );
        }
    }

    /**
     * Check for high cyclomatic complexity
     */
    private function checkHighComplexity(string $code, string $filePath, int $complexity): void
    {
        if ($complexity > 20) {
            $this->addIssue(
                $filePath,
                1,
                'major',
                'complexity',
                "High cyclomatic complexity ($complexity). Code may be difficult to test and maintain.",
                'HIGH_COMPLEXITY',
                'Refactor to reduce complexity by extracting logic into separate functions'
            );
        }
    }

    /**
     * Get complexity level based on cyclomatic complexity score
     */
    private function getComplexityLevel(int $complexity): string
    {
        if ($complexity <= 10) {
            return 'low';
        } elseif ($complexity <= 20) {
            return 'medium';
        } elseif ($complexity <= 50) {
            return 'high';
        } else {
            return 'very_high';
        }
    }

    /**
     * Get line number from offset
     */
    private function getLineNumber(string $code, int $offset): int
    {
        return substr_count(substr($code, 0, $offset), "\n") + 1;
    }

    /**
     * Add an issue to the list
     */
    private function addIssue(
        string $filePath,
        int $lineNumber,
        string $severity,
        string $category,
        string $message,
        string $ruleId,
        string $suggestedFix
    ): void {
        $this->issues[] = [
            'file_path' => $filePath,
            'line_number' => $lineNumber,
            'severity' => $severity,
            'category' => $category,
            'message' => $message,
            'rule_id' => $ruleId,
            'suggested_fix' => $suggestedFix
        ];
    }
}

?>