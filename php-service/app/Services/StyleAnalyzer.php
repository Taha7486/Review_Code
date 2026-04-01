<?php

namespace App\Services;

/**
 * Style Analysis Service
 * Checks code for style violations and best practices
 */
class StyleAnalyzer
{

    private array $issues = [];

    /**
     * Analyze code for style violations
     * 
     * @param string $code The code to analyze
     * @param string $filePath The file path (for reporting)
     * @return array Array of style issues found
     */
    public function analyze(string $code, string $filePath = 'unknown'): array
    {
        $this->issues = [];

        $this->checkNamingConventions($code, $filePath);
        $this->checkMissingDocumentation($code, $filePath);
        $this->checkIndentation($code, $filePath);
        $this->checkDeepNesting($code, $filePath);
        $this->checkLineLengths($code, $filePath);
        $this->checkStrictTypes($code, $filePath);
        $this->checkUnusedImports($code, $filePath);

        return $this->issues;
    }

    /**
     * Check for strict types declaration
     */
    private function checkStrictTypes(string $code, string $filePath): void
    {
        // Only relevant for PHP files
        if (!str_ends_with($filePath, '.php')) {
            return;
        }

        if (!preg_match('/declare\s*\(\s*strict_types\s*=\s*1\s*\)\s*;/i', $code)) {
            $this->addIssue(
                $filePath,
                1,
                'info',
                'style',
                'Missing strict_types declaration.',
                'MISSING_STRICT_TYPES',
                'Add declare(strict_types=1); at the top of the file to enforce type safety.'
            );
        }
    }

    /**
     * Check for unused imports
     */
    private function checkUnusedImports(string $code, string $filePath): void
    {
        // Find all use statements
        if (preg_match_all('/^use\s+([\w\\\\]+)(?:\s+as\s+(\w+))?\s*;/m', $code, $matches, PREG_SET_ORDER | PREG_OFFSET_CAPTURE)) {
            foreach ($matches as $match) {
                $fullClass = $match[1][0];
                $alias = $match[2][0] ?? null;
                
                // Determine the name used in the code (alias or last part of class name)
                $nameToCheck = $alias;
                if (!$nameToCheck) {
                    $parts = explode('\\', $fullClass);
                    $nameToCheck = end($parts);
                }

                // Remove the use statement itself from the search area to avoid false positives
                // matching the declaration itself.
                // We'll simplisticly search the whole code for occurrences of the name
                // excluding the exact line of the use statement.
                
                // Count occurrences
                $count = substr_count($code, $nameToCheck);
                
                // If count is 1, it might just be the use statement itself. 
                // Let's be a bit more robust: check if it appears *after* the use block.
                // Note: This is a simple heuristic.
                
                // A better approach: Regex for the name NOT preceded by 'use '
                // We need to escape the name for regex
                $safeName = preg_quote($nameToCheck, '/');
                
                // Match word boundary to avoid partial matches (e.g. usage of 'User' matching 'UserFactory')
                // Lookbehind for 'use ' is tricky in PHP PCRE if variable length, so we check unrelated usages.
                
                $usageCount = preg_match_all("/(?<!use\s)\b{$safeName}\b/", $code);

                if ($usageCount === 0) {
                     $lineNumber = $this->getLineNumber($code, $match[0][1]);
                     $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'minor',
                        'style',
                        "Unused import: '$fullClass'.",
                        'UNUSED_IMPORT',
                        "Remove the unused 'use' statement."
                    );
                }
            }
        }
    }

    /**
     * Check naming conventions
     */
    private function checkNamingConventions(string $code, string $filePath): void
    {
        // Check for non-camelCase function names
        if (preg_match_all('/function\s+([a-z]+_[a-z_]+)\s*\(/i', $code, $matches, PREG_OFFSET_CAPTURE)) {
            foreach ($matches[1] as $match) {
                $functionName = $match[0];
                $lineNumber = $this->getLineNumber($code, $match[1]);

                $this->addIssue(
                    $filePath,
                    $lineNumber,
                    'minor',
                    'style',
                    "Function '$functionName' uses snake_case. Consider using camelCase for function names.",
                    'NAMING_CONVENTION_FUNCTION',
                    "Rename to camelCase, e.g., '" . $this->snakeToCamel($functionName) . "'"
                );
            }
        }

        // Check for non-PascalCase class names
        if (preg_match_all('/class\s+([a-z][a-z_]*)\s/i', $code, $matches, PREG_OFFSET_CAPTURE)) {
            foreach ($matches[1] as $match) {
                $className = $match[0];
                if (!preg_match('/^[A-Z][a-zA-Z0-9]*$/', $className)) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);

                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'minor',
                        'style',
                        "Class '$className' should use PascalCase naming.",
                        'NAMING_CONVENTION_CLASS',
                        "Rename to PascalCase"
                    );
                }
            }
        }

        // Check for single-letter variable names (except common loop counters)
        if (preg_match_all('/\$([a-z])\b(?!\s*=\s*\d)/i', $code, $matches, PREG_OFFSET_CAPTURE)) {
            foreach ($matches[1] as $match) {
                $varName = $match[0];
                // Allow common loop counters
                if (!in_array($varName, ['i', 'j', 'k', 'x', 'y', 'z'])) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);

                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'info',
                        'style',
                        "Variable '\$$varName' is too short. Use descriptive variable names.",
                        'SHORT_VARIABLE_NAME',
                        "Use a more descriptive name that explains the variable's purpose"
                    );
                }
            }
        }
    }

    /**
     * Check for missing documentation
     */
    private function checkMissingDocumentation(string $code, string $filePath): void
    {
        // Find all public functions
        if (preg_match_all('/public\s+function\s+(\w+)\s*\([^)]*\)/i', $code, $matches, PREG_OFFSET_CAPTURE)) {
            foreach ($matches[0] as $index => $match) {
                $functionName = $matches[1][$index][0];
                $offset = $match[1];

                // Check if there's a docblock before this function
                $beforeFunction = substr($code, max(0, $offset - 200), 200);
                if (!preg_match('/\/\*\*.*?\*\//s', $beforeFunction)) {
                    $lineNumber = $this->getLineNumber($code, $offset);

                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'minor',
                        'style',
                        "Public function '$functionName' is missing PHPDoc documentation.",
                        'MISSING_DOCUMENTATION',
                        "Add a PHPDoc comment describing the function's purpose, parameters, and return value"
                    );
                }
            }
        }

        // Find all classes
        if (preg_match_all('/class\s+(\w+)/i', $code, $matches, PREG_OFFSET_CAPTURE)) {
            foreach ($matches[0] as $index => $match) {
                $className = $matches[1][$index][0];
                $offset = $match[1];

                // Check if there's a docblock before this class
                $beforeClass = substr($code, max(0, $offset - 200), 200);
                if (!preg_match('/\/\*\*.*?\*\//s', $beforeClass)) {
                    $lineNumber = $this->getLineNumber($code, $offset);

                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'minor',
                        'style',
                        "Class '$className' is missing PHPDoc documentation.",
                        'MISSING_CLASS_DOCUMENTATION',
                        "Add a PHPDoc comment describing the class's purpose"
                    );
                }
            }
        }
    }

    /**
     * Check indentation consistency
     */
    private function checkIndentation(string $code, string $filePath): void
    {
        $lines = explode("\n", $code);
        $hasSpaces = false;
        $hasTabs = false;

        foreach ($lines as $lineNum => $line) {
            if (preg_match('/^    /', $line)) {
                $hasSpaces = true;
            }
            if (preg_match('/^\t/', $line)) {
                $hasTabs = true;
            }

            if ($hasSpaces && $hasTabs) {
                $this->addIssue(
                    $filePath,
                    $lineNum + 1,
                    'minor',
                    'style',
                    'Inconsistent indentation detected. Mix of tabs and spaces.',
                    'INCONSISTENT_INDENTATION',
                    'Use either spaces or tabs consistently throughout the file (PSR-12 recommends 4 spaces)'
                );
                break; // Only report once per file
            }
        }
    }

    /**
     * Check for deep nesting levels
     */
    private function checkDeepNesting(string $code, string $filePath): void
    {
        $lines = explode("\n", $code);
        $maxNesting = 4; // PSR-12 recommendation

        foreach ($lines as $lineNum => $line) {
            // Count opening braces at start of line (rough nesting indicator)
            $nestingLevel = 0;
            $trimmed = ltrim($line);
            $indent = strlen($line) - strlen($trimmed);

            // Estimate nesting by indentation (assuming 4 spaces per level)
            $nestingLevel = intdiv($indent, 4);

            if ($nestingLevel > $maxNesting) {
                $this->addIssue(
                    $filePath,
                    $lineNum + 1,
                    'major',
                    'complexity',
                    "Deep nesting detected (level $nestingLevel). Consider refactoring.",
                    'DEEP_NESTING',
                    'Extract nested logic into separate functions to improve readability'
                );
            }
        }
    }

    /**
     * Check line lengths
     */
    private function checkLineLengths(string $code, string $filePath): void
    {
        $lines = explode("\n", $code);
        $maxLength = 120; // PSR-12 soft limit

        foreach ($lines as $lineNum => $line) {
            $length = strlen($line);
            if ($length > $maxLength) {
                $this->addIssue(
                    $filePath,
                    $lineNum + 1,
                    'info',
                    'style',
                    "Line exceeds $maxLength characters ($length characters).",
                    'LINE_TOO_LONG',
                    'Break long lines into multiple lines for better readability'
                );
            }
        }
    }

    /**
     * Check for trailing whitespace
     */
    private function checkTrailingWhitespace(string $code, string $filePath): void
    {
        $lines = explode("\n", $code);

        foreach ($lines as $lineNum => $line) {
            if (preg_match('/\s+$/', $line)) {
                $this->addIssue(
                    $filePath,
                    $lineNum + 1,
                    'info',
                    'style',
                    'Trailing whitespace detected.',
                    'TRAILING_WHITESPACE',
                    'Remove trailing spaces and tabs'
                );
            }
        }
    }

    /**
     * Convert snake_case to camelCase
     */
    private function snakeToCamel(string $string): string
    {
        return lcfirst(str_replace('_', '', ucwords($string, '_')));
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