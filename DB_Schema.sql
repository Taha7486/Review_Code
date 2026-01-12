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
    username VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6)
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
-- TABLE: analysis_runs
-- ================================================
CREATE TABLE analysis_runs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    repository_id INT NOT NULL,
    branch_name VARCHAR(255) NOT NULL,
    default_branch VARCHAR(255) NOT NULL,
    base_commit_sha VARCHAR(40) NOT NULL,
    head_commit_sha VARCHAR(40) NOT NULL,
    status VARCHAR(50) NOT NULL,
    files_analyzed INT NOT NULL DEFAULT 0,
    average_score DECIMAL(5,2) NOT NULL DEFAULT 0,
    total_issues INT NOT NULL DEFAULT 0,
    summary TEXT NULL,
    raw_output LONGTEXT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    completed_at DATETIME(6) NULL,

    INDEX idx_runs_repo_branch (repository_id, branch_name),
    UNIQUE INDEX idx_runs_repo_commits (repository_id, base_commit_sha, head_commit_sha),
    INDEX idx_runs_status (status),

    CONSTRAINT fk_runs_repo
        FOREIGN KEY (repository_id) REFERENCES repositories(id)
        ON DELETE CASCADE
);

-- ================================================
-- TABLE: analysis_issues
-- ================================================
CREATE TABLE analysis_issues (
    id INT AUTO_INCREMENT PRIMARY KEY,
    run_id INT NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    line_start INT NULL,
    line_end INT NULL,
    severity VARCHAR(50) NOT NULL,
    category VARCHAR(100) NOT NULL,
    message TEXT NOT NULL,
    rule_id VARCHAR(100) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    INDEX idx_ai_run (run_id),
    INDEX idx_ai_run_sev (run_id, severity),

    CONSTRAINT fk_ai_run
        FOREIGN KEY (run_id) REFERENCES analysis_runs(id)
        ON DELETE CASCADE
);

-- ================================================
-- TABLE: analysis_metrics
-- ================================================
CREATE TABLE analysis_metrics (
    id INT AUTO_INCREMENT PRIMARY KEY,
    run_id INT NOT NULL,
    metrics_json JSON NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    INDEX idx_am_run (run_id),

    CONSTRAINT fk_am_run
        FOREIGN KEY (run_id) REFERENCES analysis_runs(id)
        ON DELETE CASCADE
);
