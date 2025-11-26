<?php

namespace App\Controllers;
class HealthController {

    public function getHealth($request, $response) 
    {

        $data = json_encode(['status' => 'ok', 'service' => 'php-analyzer']);
        
        $response->getBody()->write($data);
        return $response->withHeader('Content-Type', 'application/json');
    }
    
}
?>