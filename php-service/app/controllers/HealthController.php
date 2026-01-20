<?php
namespace App\Controllers;

use Psr\Http\Message\ResponseInterface as Response;
use Psr\Http\Message\ServerRequestInterface as Request;

class HealthController
{
    public function check(Request $request, Response $response): Response
    {
        $status = [
            'status' => 'ok',
            'service' => 'php-analysis-engine',
            'timestamp' => date('c'),
            'memory_usage' => memory_get_usage(true),
            'version' => '1.0.0'
        ];

        $response->getBody()->write(json_encode($status));
        return $response->withHeader('Content-Type', 'application/json');
    }
}