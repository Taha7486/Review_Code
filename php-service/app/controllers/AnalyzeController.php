<?php
namespace App\Controllers;

use App\Services\ComplexityAnalyzeService;

class AnalyzeController {

    private $complexityService;

    public function __construct() {
        $this->complexityService = new ComplexityAnalyzeService();
    }

    public function analyzeCode($request, $response) {
        $body = $request->getParsedBody();
        $code = $body['code'] ?? null;

        if (!$code) {
            $error = json_encode(['success' => false , 'error'=> 'Code required']);
            $response->getBody()->write($error);
            return $response->withHeader('Content-Type', 'application/json')->withStatus(400);
            
        }
        
        // Analyze code using the ComplexityAnalyzeService
        $analysis = $this->complexityService->analyzeCode($code);

        $result = json_encode([
            'success'=> true , 
            'data'=>[
                'cyclomatic_complexity' => $analysis['cyclomatic_complexity'],
                'complexity_level' => $analysis['complexity_level'],
                'code_length' => $analysis['code_length'],
                'lines_of_code' => $analysis['lines_of_code'],
                'function_count' => $analysis['function_count'],
                'class_count' => $analysis['class_count']
            ]
        ]);

        $response->getBody()->write($result);
        return $response->withHeader('Content-Type', 'application/json');
    }




}
?>