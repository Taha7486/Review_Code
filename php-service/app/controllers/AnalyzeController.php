<?php
namespace App\Controllers;

use App\Services\CodeAnalysisService;
use Psr\Http\Message\ResponseInterface as Response;
use Psr\Http\Message\ServerRequestInterface as Request;

/**
 * Analyze Controller
 * Handles code analysis API endpoints
 */
class AnalyzeController
{

    private CodeAnalysisService $analysisService;

    public function __construct()
    {
        $this->analysisService = new CodeAnalysisService();
    }

    /**
     * Analyze single code snippet
     * POST /api/analyze/code
     * Body: { "code": "...", "file_path": "optional" }
     */
    public function analyzeCode(Request $request, Response $response): Response
    {
        $correlationId = $request->getAttribute('correlation_id');
        $body = $request->getParsedBody();
        $code = $body['code'] ?? null;
        $filePath = $body['file_path'] ?? 'unknown';

        if (!$code) {
            $error = json_encode([
                'code' => 'MISSING_REQUIRED_FIELD',
                'message' => 'Code content is required for analysis',
                'correlationId' => $correlationId
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(400);
        }

        try {
            // Analyze the code
            $result = $this->analysisService->analyzeCode($code, $filePath);

            $responseData = json_encode($result);
            $response->getBody()->write($responseData);

            return $response->withHeader('Content-Type', 'application/json');

        } catch (\Exception $e) {
            $error = json_encode([
                'code' => 'ANALYSIS_FAILED',
                'message' => 'Analysis failed: ' . $e->getMessage(),
                'correlationId' => $correlationId
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(500);
        }
    }

    /**
     * Analyze multiple files
     * POST /api/analyze/files
     * Body: { "files": [{ "path": "...", "content": "..." }] }
     */
    public function analyzeFiles(Request $request, Response $response): Response
    {
        $correlationId = $request->getAttribute('correlation_id');
        $body = $request->getParsedBody();
        $files = $body['files'] ?? null;

        // Validation limits
        $maxFiles = 50;
        $maxFileSize = 1024 * 500; // 500KB

        if (!$files || !is_array($files)) {
            $error = json_encode([
                'code' => 'INVALID_REQUEST',
                'message' => 'Files array is required',
                'correlationId' => $correlationId
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(400);
        }

        if (count($files) > $maxFiles) {
            $error = json_encode([
                'code' => 'FILE_LIMIT_EXCEEDED',
                'message' => "Too many files. Maximum allowed is $maxFiles.",
                'correlationId' => $correlationId
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(400);
        }

        foreach ($files as $file) {
            if (strlen($file['content'] ?? '') > $maxFileSize) {
                $error = json_encode([
                    'code' => 'FILE_SIZE_EXCEEDED',
                    'message' => "File '{$file['path']}' exceeds size limit of 500KB.",
                    'correlationId' => $correlationId
                ]);
                $response->getBody()->write($error);
                return $response
                    ->withHeader('Content-Type', 'application/json')
                    ->withStatus(400);
            }
        }

        try {
            // Analyze all files
            $result = $this->analysisService->analyzeMultipleFiles($files);

            $responseData = json_encode($result);
            $response->getBody()->write($responseData);

            return $response->withHeader('Content-Type', 'application/json');

        } catch (\Exception $e) {
            $error = json_encode([
                'code' => 'ANALYSIS_FAILED',
                'message' => 'Analysis failed: ' . $e->getMessage(),
                'correlationId' => $correlationId
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(500);
        }
    }
}
?>