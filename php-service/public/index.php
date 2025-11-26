<?php
require __DIR__ . '/../vendor/autoload.php'; // load the classes of slim framework

use Slim\Factory\AppFactory;

$app = AppFactory::create(); // create the application

$app->addBodyParsingMiddleware();

require __DIR__ . '/../app/routes.php'; // load the routes of the application

$app->run(); // run the application

?>