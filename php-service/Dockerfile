# php:8.3-cli-alpine
FROM php:8.3-cli-alpine@sha256:a8a81d4ff4ea9c1306b1369af4150a4a46198c18b536061f5f9f06e39ffa09f8
# composer:latest
COPY --from=composer:latest@sha256:c883af18892268b3b8369c4a39c08f80b393383e79d80b75140a3ea489dbbb78 /usr/bin/composer /usr/bin/composer
WORKDIR /var/www
RUN apk add --no-cache curl
COPY composer.json composer.lock ./
RUN composer install --no-dev
COPY . .
RUN composer dump-autoload --optimize
EXPOSE 8000
CMD ["php", "-S", "0.0.0.0:8000", "-t", "public"]