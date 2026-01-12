/**
 * JWT utility functions for decoding and validating tokens
 */

/**
 * Decode a JWT token without verification (client-side only)
 * @param {string} token - JWT token string
 * @returns {object|null} Decoded payload or null if invalid
 */
export const decodeJWT = (token) => {
    if (!token) return null;
    
    try {
        const parts = token.split('.');
        if (parts.length !== 3) return null;
        
        // Decode the payload (second part)
        const payload = parts[1];
        const decoded = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
        
        return decoded;
    } catch (error) {
        console.error('Failed to decode JWT:', error);
        return null;
    }
};

/**
 * Check if a JWT token is expired
 * @param {string} token - JWT token string
 * @returns {boolean} True if token is expired or invalid
 */
export const isTokenExpired = (token) => {
    const decoded = decodeJWT(token);
    if (!decoded || !decoded.exp) return true;
    
    // exp is in seconds, Date.now() is in milliseconds
    const expirationTime = decoded.exp * 1000;
    const now = Date.now();
    
    // Add 5 second buffer to account for clock skew
    return now >= (expirationTime - 5000);
};

/**
 * Get expiration time in milliseconds from now
 * @param {string} token - JWT token string
 * @returns {number|null} Milliseconds until expiration, or null if invalid
 */
export const getTokenExpirationTime = (token) => {
    const decoded = decodeJWT(token);
    if (!decoded || !decoded.exp) return null;
    
    const expirationTime = decoded.exp * 1000;
    const now = Date.now();
    
    return Math.max(0, expirationTime - now);
};

/**
 * Extract user info from JWT token
 * @param {string} token - JWT token string
 * @returns {object|null} User info with id, name, email, or null if invalid
 */
export const getUserFromToken = (token) => {
    const decoded = decodeJWT(token);
    if (!decoded) return null;
    
    return {
        id: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || decoded.sub || decoded.nameid,
        name: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || decoded.name || decoded.unique_name,
        email: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || decoded.email,
        jti: decoded.jti, // JWT ID
        exp: decoded.exp, // Expiration timestamp
        iat: decoded.iat // Issued at timestamp
    };
};

/**
 * Schedule a callback to run when the token expires
 * @param {string} token - JWT token string
 * @param {Function} callback - Function to call when token expires
 * @returns {number|null} Timeout ID that can be used to clear the schedule, or null if invalid
 */
export const scheduleTokenExpiration = (token, callback) => {
    const expirationTime = getTokenExpirationTime(token);
    if (expirationTime === null) return null;
    
    if (expirationTime <= 0) {
        // Token already expired, call callback immediately
        callback();
        return null;
    }
    
    // Schedule callback 5 seconds before expiration to be safe
    const scheduleTime = Math.max(0, expirationTime - 5000);
    
    return setTimeout(() => {
        callback();
    }, scheduleTime);
};
