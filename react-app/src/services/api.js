import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5116/api';

const api = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Add token to requests if available
api.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('token');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

// Store logout callback for 401 handling
let logoutCallback = null;

/**
 * Setup function to configure the API interceptor with logout callback
 * Should be called from App.js or AuthProvider to enable proper logout
 * @param {Function} logoutFn - Function to call when 401 is detected
 */
export const setupApiInterceptors = (logoutFn) => {
    logoutCallback = logoutFn;
};

// Response interceptor to handle errors globally with standardized error messages
api.interceptors.response.use(
    (response) => {
        // If response is successful, just return it
        return response;
    },
    (error) => {
        // Extract standardized error response
        const errorResponse = error.response?.data;
        const status = error.response?.status;
        const errorCode = errorResponse?.code;
        const errorMessage = errorResponse?.message || error.message || 'An unexpected error occurred';

        // Handle 401 Unauthorized errors
        if (status === 401) {
            const requestUrl = error.config?.url || '';
            const isAuthEndpoint = requestUrl.includes('/auth/login') || requestUrl.includes('/auth/register');

            // Don't auto-logout for login/register endpoints (invalid credentials are expected)
            if (!isAuthEndpoint) {
                // Clear authentication data from localStorage
                localStorage.removeItem('token');

                // Call logout callback if provided (for proper state cleanup)
                if (logoutCallback && typeof logoutCallback === 'function') {
                    try {
                        logoutCallback();
                    } catch (err) {
                        if (process.env.NODE_ENV === 'development') {
                            console.error('Error calling logout callback:', err);
                        }
                    }
                }

                // Only redirect if we're not already on the login/register page
                const currentPath = window.location.pathname;
                if (currentPath !== '/login' && currentPath !== '/register') {
                    // Use window.location for full page reload to ensure clean state
                    // This ensures AuthContext is re-initialized without token
                    window.location.href = '/login';
                }

                // Log warning for debugging
                if (process.env.NODE_ENV === 'development') {
                    console.warn('Session expired. Please log in again.');
                }
            }
        }

        // Enhance error object with standardized error information
        const enhancedError = {
            ...error,
            message: errorMessage,
            code: errorCode,
            status: status,
            details: errorResponse?.details,
            correlationId: errorResponse?.correlationId
        };

        // Log error for debugging (in development)
        if (process.env.NODE_ENV === 'development') {
            console.error('API Error:', {
                url: error.config?.url,
                method: error.config?.method,
                status,
                code: errorCode,
                message: errorMessage,
                correlationId: errorResponse?.correlationId
            });
        }

        // Return the enhanced error so it can be handled by the calling code
        return Promise.reject(enhancedError);
    }
);

export const authService = {
    register: async (name, email, password, githubToken = null) => {
        const payload = { name, email, password };
        if (githubToken) {
            payload.githubToken = githubToken;
        }
        const response = await api.post('/auth/register', payload);
        return response.data;
    },

    login: async (email, password) => {
        const response = await api.post('/auth/login', { email, password });
        return response.data;
    },
};

export const analysisService = {
    analyzeBranch: async (repoUrl, branchName, githubToken = null) => {
        const payload = { repoUrl, branchName };
        if (githubToken) {
            payload.githubToken = githubToken;
        }
        const response = await api.post('/analysis/analyze', payload);
        // New async pattern: returns { runId, status, message } immediately
        // Client should poll /runs/{runId} for status
        return response.data;
    },
    getBranches: async (repoUrl, githubToken = null) => {
        let url = `/analysis/branches?repoUrl=${encodeURIComponent(repoUrl)}`;
        if (githubToken) {
            url += `&githubToken=${encodeURIComponent(githubToken)}`;
        }
        const response = await api.get(url);
        return response.data;
    },
    getAnalysisRuns: async (repoUrl = null, branchName = null, limit = 20) => {
        const params = new URLSearchParams();
        if (repoUrl) params.append('repoUrl', repoUrl);
        if (branchName) params.append('branchName', branchName);
        params.append('limit', limit.toString());
        const response = await api.get(`/analysis/runs?${params.toString()}`);
        return response.data;
    },
    getAnalysisRunById: async (id) => {
        const response = await api.get(`/analysis/runs/${id}`);
        return response.data;
    },
    getAnalysisRunIssues: async (id, severity = null, category = null) => {
        const params = new URLSearchParams();
        if (severity) params.append('severity', severity);
        if (category) params.append('category', category);
        const query = params.toString();
        const response = await api.get(`/analysis/runs/${id}/issues${query ? `?${query}` : ''}`);
        return response.data;
    }
};

export default api;
