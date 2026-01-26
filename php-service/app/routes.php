<?php
use App\Controllers\HealthController;
use App\Controllers\AnalyzeController;
use App\Controllers\MetricsController;

// Health check
$app->get('/health', [App\Controllers\HealthController::class, 'check']);

// Prometheus metrics
$app->get('/metrics', [MetricsController::class, 'metrics']);

// Code analysis endpoints
$app->post('/api/analyze/code', [AnalyzeController::class, 'analyzeCode']);
$app->post('/api/analyze/files', [AnalyzeController::class, 'analyzeFiles']);

?>