-- ================================================
-- CREATE DATABASE
-- ================================================
CREATE DATABASE IF NOT EXISTS code_review_tool
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE code_review_tool;

-- ================================================
-- TABLE: users
-- ================================================
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    github_user_id BIGINT NULL,
    username VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    avatar_url VARCHAR(500),

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6),

    INDEX idx_users_github_user_id (github_user_id)
);

-- ================================================
-- TABLE: repositories
-- ================================================
CREATE TABLE repositories (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    github_repo_id BIGINT NOT NULL,
    name VARCHAR(255) NOT NULL,
    full_name VARCHAR(300) NOT NULL,
    clone_url VARCHAR(500) NOT NULL,
    provider VARCHAR(50) NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6),

    UNIQUE INDEX idx_repos_github_repo_id (github_repo_id),

    CONSTRAINT fk_repos_user
        FOREIGN KEY (user_id) REFERENCES users(id)
        ON DELETE CASCADE
);

-- ================================================
-- TABLE: pull_requests
-- ================================================
CREATE TABLE pull_requests (
    id INT AUTO_INCREMENT PRIMARY KEY,
    repository_id INT NOT NULL,
    github_pr_id BIGINT NOT NULL,
    number INT NOT NULL,
    title VARCHAR(500) NOT NULL,
    state VARCHAR(50) NOT NULL,
    author_user_id INT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6),

    UNIQUE INDEX idx_pr_repo_number (repository_id, number),

    CONSTRAINT fk_pr_repo
        FOREIGN KEY (repository_id) REFERENCES repositories(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_pr_author
        FOREIGN KEY (author_user_id) REFERENCES users(id)
);

-- ================================================
-- TABLE: code_reviews
-- ================================================
CREATE TABLE code_reviews (
    id INT AUTO_INCREMENT PRIMARY KEY,
    pull_request_id INT NOT NULL,
    status VARCHAR(50) NOT NULL,
    summary TEXT NULL,
    raw_output JSON NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    completed_at DATETIME(6) NULL,

    INDEX idx_reviews_pr_id (pull_request_id),

    CONSTRAINT fk_review_pr
        FOREIGN KEY (pull_request_id) REFERENCES pull_requests(id)
        ON DELETE CASCADE
);

-- ================================================
-- TABLE: review_issues
-- ================================================
CREATE TABLE review_issues (
    id INT AUTO_INCREMENT PRIMARY KEY,
    review_id INT NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    line_start INT NULL,
    line_end INT NULL,

    severity VARCHAR(50) NOT NULL,
    type VARCHAR(100) NOT NULL,
    message TEXT NOT NULL,
    rule_id VARCHAR(100) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    INDEX idx_issues_review_id (review_id),

    CONSTRAINT fk_issue_review
        FOREIGN KEY (review_id) REFERENCES code_reviews(id)
        ON DELETE CASCADE
);

-- ================================================
-- TABLE: metrics
-- ================================================
CREATE TABLE metrics (
    id INT AUTO_INCREMENT PRIMARY KEY,
    review_id INT NOT NULL,
    metrics_json JSON NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    INDEX idx_metrics_review_id (review_id),

    CONSTRAINT fk_metrics_review
        FOREIGN KEY (review_id) REFERENCES code_reviews(id)
        ON DELETE CASCADE
);

-- ================================================
-- TABLE: review_configurations
-- ================================================
CREATE TABLE review_configurations (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    config_json JSON NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6),

    CONSTRAINT fk_config_user
        FOREIGN KEY (user_id) REFERENCES users(id)
        ON DELETE CASCADE
);
