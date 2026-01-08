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
        $body = $request->getParsedBody();
        $code = $body['code'] ?? null;
        $filePath = $body['file_path'] ?? 'unknown';

        if (!$code) {
            $error = json_encode([
                'success' => false,
                'error' => 'Code is required'
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
                'success' => false,
                'error' => 'Analysis failed: ' . $e->getMessage()
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
        $body = $request->getParsedBody();
        $files = $body['files'] ?? null;

        if (!$files || !is_array($files)) {
            $error = json_encode([
                'success' => false,
                'error' => 'Files array is required'
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(400);
        }

        try {
            // Analyze all files
            $result = $this->analysisService->analyzeMultipleFiles($files);

            $responseData = json_encode($result);
            $response->getBody()->write($responseData);

            return $response->withHeader('Content-Type', 'application/json');

        } catch (\Exception $e) {
            $error = json_encode([
                'success' => false,
                'error' => 'Analysis failed: ' . $e->getMessage()
            ]);
            $response->getBody()->write($error);
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(500);
        }
    }
}
?>