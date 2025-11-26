<?php

namespace App\Services;

class ComplexityAnalyzeService {
    
    
    public function calculateCyclomaticComplexity(string $code): int {
        // Base complexity is 1
        $complexity = 1;
        
        // Tokenize the code
        $tokens = token_get_all($code);
        $tokenCount = count($tokens);
        
        for ($i = 0; $i < $tokenCount; $i++) {
            $token = $tokens[$i];
            
            // Skip if token is an array (tokenized element)
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
                    
                case T_INLINE_HTML:
                    // Skip HTML content
                    break;
            }
            
            // Check for ternary operator (? :)
            // Only count the ? part to avoid double counting
            if ($tokenValue === '?') {
                // Make sure it's a ternary operator, not part of a namespace or null coalescing
                if ($i > 0 && $i < $tokenCount - 1) {
                    $prevToken = is_array($tokens[$i - 1]) ? $tokens[$i - 1][1] : $tokens[$i - 1];
                    
                    // Check if it's not a null coalescing operator (??)
                    $isNullCoalescing = false;
                    if ($i < $tokenCount - 1) {
                        $nextToken = is_array($tokens[$i + 1]) ? $tokens[$i + 1][1] : $tokens[$i + 1];
                        if ($nextToken === '?') {
                            $isNullCoalescing = true;
                        }
                    }
                    
                    // Count as ternary if it's not null coalescing and not in a problematic context
                    if (!$isNullCoalescing && !in_array($prevToken, ['?', ':', ';', '{', '}', '(', ')', '['])) {
                        $complexity++;
                    }
                }
            }
        }
        
        return $complexity;
    }
    
    /**
     * Analyze code and return comprehensive complexity metrics
     * 
     * @param string $code The PHP code to analyze
     * @return array Array containing complexity metrics
     */
    public function analyzeCode(string $code): array {
        $cyclomaticComplexity = $this->calculateCyclomaticComplexity($code);
        $codeLength = strlen($code);
        $linesOfCode = substr_count($code, "\n") + 1;
        
        // Count various code elements
        $functionCount = preg_match_all('/\bfunction\s+\w+\s*\(/i', $code);
        $classCount = preg_match_all('/\bclass\s+\w+/i', $code);
        
        return [
            'cyclomatic_complexity' => $cyclomaticComplexity,
            'code_length' => $codeLength,
            'lines_of_code' => $linesOfCode,
            'function_count' => $functionCount,
            'class_count' => $classCount,
            'complexity_level' => $this->getComplexityLevel($cyclomaticComplexity)
        ];
    }
    
    /**
     * Get complexity level based on cyclomatic complexity score
     * 
     * @param int $complexity The cyclomatic complexity score
     * @return string The complexity level (low, medium, high, very_high)
     */
    private function getComplexityLevel(int $complexity): string {
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
}

?>