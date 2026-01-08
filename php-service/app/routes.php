<?php
use App\Controllers\HealthController;
use App\Controllers\AnalyzeController;

// Health check
$app->get('/health', [HealthController::class, 'getHealth']);

// Code analysis endpoints
$app->post('/api/analyze/code', [AnalyzeController::class, 'analyzeCode']);
$app->post('/api/analyze/files', [AnalyzeController::class, 'analyzeFiles']);

?>