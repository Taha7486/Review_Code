<?php
require __DIR__ . '/../vendor/autoload.php';

use Slim\Factory\AppFactory;
use Psr\Http\Message\ServerRequestInterface as Request;
use Slim\Psr7\Response;

// Production settings
$isProduction = (getenv('APP_ENV') === 'production');
ini_set('display_errors', $isProduction ? '0' : '1');
ini_set('log_errors', '1');

$app = AppFactory::create();

$app->addBodyParsingMiddleware();

// 🔐 Authentication Middleware (Shared Secret)
$app->add(function (Request $request, $handler) {
    $expectedSecret = getenv('INTERNAL_SERVICE_SECRET');

    // In development or if no secret is set, we bypass (optional, but safer to require it)
    if (!empty($expectedSecret) && $expectedSecret !== 'change_me_in_production') {
        $authHeader = $request->getHeaderLine('Authorization');
        $providedSecret = '';

        if (preg_match('/Bearer\s+(.*)$/i', $authHeader, $matches)) {
            $providedSecret = $matches[1];
        }

        if ($providedSecret !== $expectedSecret) {
            $response = new Response();
            $response->getBody()->write(json_encode([
                'success' => false,
                'error' => 'Unauthorized: Invalid internal service token',
                'code' => 'UNAUTHORIZED'
            ]));
            return $response
                ->withHeader('Content-Type', 'application/json')
                ->withStatus(401);
        }
    }

    return $handler->handle($request);
});

// Correlation ID Middleware
$app->add(function ($request, $handler) {
    $correlationId = $request->getHeaderLine('X-Correlation-Id');
    if (empty($correlationId)) {
        $correlationId = uniqid('php-', true);
    }

    $request = $request->withAttribute('correlation_id', $correlationId);
    $response = $handler->handle($request);

    return $response->withHeader('X-Correlation-Id', $correlationId);
});

require __DIR__ . '/../app/routes.php';

$app->run();
?>