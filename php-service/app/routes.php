<?php
use App\Controllers\HealthController;
use App\Controllers\AnalyzeController;

$app->get('/health', [HealthController::class, 'getHealth']);
$app->post('/api/analyze/code', [AnalyzeController::class, 'analyzeCode']);


?>