FROM php:8.2-cli-alpine
WORKDIR /var/www
RUN apk add --no-cache composer curl
COPY composer.json composer.lock ./
RUN composer install --no-dev
COPY . .
RUN composer dump-autoload --optimize
EXPOSE 8000
CMD ["php", "-S", "0.0.0.0:8000", "-t", "public"]