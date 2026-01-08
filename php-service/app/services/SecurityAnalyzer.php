<?php

namespace App\Services;

/**
 * Security Analysis Service
 * Detects common security vulnerabilities in code
 */
class SecurityAnalyzer {
    
    private array $issues = [];
    
    /**
     * Analyze code for security vulnerabilities
     * 
     * @param string $code The code to analyze
     * @param string $filePath The file path (for reporting)
     * @return array Array of security issues found
     */
    public function analyze(string $code, string $filePath = 'unknown'): array {
        $this->issues = [];
        
        $this->checkSqlInjection($code, $filePath);
        $this->checkXssVulnerabilities($code, $filePath);
        $this->checkHardcodedSecrets($code, $filePath);
        $this->checkInsecureRandom($code, $filePath);
        $this->checkPathTraversal($code, $filePath);
        $this->checkCommandInjection($code, $filePath);
        $this->checkFileInclusion($code, $filePath);
        
        return $this->issues;
    }
    
    /**
     * Check for SQL injection vulnerabilities
     */
    private function checkSqlInjection(string $code, string $filePath): void {
        // Pattern: Direct variable concatenation in SQL queries
        $patterns = [
            '/\$\w+\s*=\s*["\']SELECT.*?\$\w+.*?["\']/i',
            '/\$\w+\s*=\s*["\']INSERT.*?\$\w+.*?["\']/i',
            '/\$\w+\s*=\s*["\']UPDATE.*?\$\w+.*?["\']/i',
            '/\$\w+\s*=\s*["\']DELETE.*?\$\w+.*?["\']/i',
            '/mysql_query\s*\(\s*["\'].*?\$\w+/i',
            '/mysqli_query\s*\(.*?["\'].*?\$\w+/i',
        ];
        
        foreach ($patterns as $pattern) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'critical',
                        'security',
                        'Potential SQL Injection vulnerability detected. Use prepared statements instead.',
                        'SQL_INJECTION',
                        'Use PDO prepared statements or mysqli_prepare() to prevent SQL injection.'
                    );
                }
            }
        }
    }
    
    /**
     * Check for XSS vulnerabilities
     */
    private function checkXssVulnerabilities(string $code, string $filePath): void {
        // Pattern: Unescaped output
        $patterns = [
            '/echo\s+\$_(GET|POST|REQUEST|COOKIE)\[/i',
            '/print\s+\$_(GET|POST|REQUEST|COOKIE)\[/i',
            '/\?>\s*\$_(GET|POST|REQUEST|COOKIE)\[/i',
        ];
        
        foreach ($patterns as $pattern) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'critical',
                        'security',
                        'Potential XSS vulnerability. User input is echoed without sanitization.',
                        'XSS_VULNERABILITY',
                        'Use htmlspecialchars() or htmlentities() to escape output.'
                    );
                }
            }
        }
    }
    
    /**
     * Check for hardcoded secrets
     */
    private function checkHardcodedSecrets(string $code, string $filePath): void {
        $patterns = [
            '/password\s*=\s*["\'][^"\']{3,}["\']/i' => 'Hardcoded password detected',
            '/api[_-]?key\s*=\s*["\'][^"\']{10,}["\']/i' => 'Hardcoded API key detected',
            '/secret\s*=\s*["\'][^"\']{10,}["\']/i' => 'Hardcoded secret detected',
            '/token\s*=\s*["\'][^"\']{10,}["\']/i' => 'Hardcoded token detected',
            '/private[_-]?key\s*=\s*["\'][^"\']{10,}["\']/i' => 'Hardcoded private key detected',
        ];
        
        foreach ($patterns as $pattern => $message) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'critical',
                        'security',
                        $message . '. Store sensitive data in environment variables.',
                        'HARDCODED_SECRET',
                        'Move secrets to .env file and use getenv() or $_ENV to access them.'
                    );
                }
            }
        }
    }
    
    /**
     * Check for insecure random number generation
     */
    private function checkInsecureRandom(string $code, string $filePath): void {
        $patterns = [
            '/\brand\s*\(/i',
            '/\bmt_rand\s*\(/i',
        ];
        
        foreach ($patterns as $pattern) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'major',
                        'security',
                        'Insecure random number generation. Use random_int() or random_bytes() for security-sensitive operations.',
                        'INSECURE_RANDOM',
                        'Replace rand()/mt_rand() with random_int() for cryptographic purposes.'
                    );
                }
            }
        }
    }
    
    /**
     * Check for path traversal vulnerabilities
     */
    private function checkPathTraversal(string $code, string $filePath): void {
        $patterns = [
            '/file_get_contents\s*\(\s*\$_(GET|POST|REQUEST)/i',
            '/fopen\s*\(\s*\$_(GET|POST|REQUEST)/i',
            '/include\s+\$_(GET|POST|REQUEST)/i',
            '/require\s+\$_(GET|POST|REQUEST)/i',
        ];
        
        foreach ($patterns as $pattern) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'critical',
                        'security',
                        'Potential path traversal vulnerability. User input used in file operations.',
                        'PATH_TRAVERSAL',
                        'Validate and sanitize file paths. Use basename() and realpath() to prevent directory traversal.'
                    );
                }
            }
        }
    }
    
    /**
     * Check for command injection vulnerabilities
     */
    private function checkCommandInjection(string $code, string $filePath): void {
        $patterns = [
            '/exec\s*\(\s*["\'].*?\$\w+/i',
            '/shell_exec\s*\(\s*["\'].*?\$\w+/i',
            '/system\s*\(\s*["\'].*?\$\w+/i',
            '/passthru\s*\(\s*["\'].*?\$\w+/i',
        ];
        
        foreach ($patterns as $pattern) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'critical',
                        'security',
                        'Potential command injection vulnerability. User input used in system command.',
                        'COMMAND_INJECTION',
                        'Use escapeshellarg() and escapeshellcmd() or avoid shell commands entirely.'
                    );
                }
            }
        }
    }
    
    /**
     * Check for file inclusion vulnerabilities
     */
    private function checkFileInclusion(string $code, string $filePath): void {
        $patterns = [
            '/include\s*\(\s*\$_(GET|POST|REQUEST|COOKIE)/i',
            '/require\s*\(\s*\$_(GET|POST|REQUEST|COOKIE)/i',
            '/include_once\s*\(\s*\$_(GET|POST|REQUEST|COOKIE)/i',
            '/require_once\s*\(\s*\$_(GET|POST|REQUEST|COOKIE)/i',
        ];
        
        foreach ($patterns as $pattern) {
            if (preg_match_all($pattern, $code, $matches, PREG_OFFSET_CAPTURE)) {
                foreach ($matches[0] as $match) {
                    $lineNumber = $this->getLineNumber($code, $match[1]);
                    $this->addIssue(
                        $filePath,
                        $lineNumber,
                        'critical',
                        'security',
                        'Potential file inclusion vulnerability. User input used in include/require.',
                        'FILE_INCLUSION',
                        'Never use user input directly in include/require. Use a whitelist of allowed files.'
                    );
                }
            }
        }
    }
    
    /**
     * Get line number from offset
     */
    private function getLineNumber(string $code, int $offset): int {
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
