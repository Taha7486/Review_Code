import React, { createContext, useContext, useState, useEffect, useRef, useCallback } from 'react';
import { getUserFromToken, isTokenExpired, scheduleTokenExpiration } from '../utils/jwt';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(null);
    const [loading, setLoading] = useState(true);
    const expirationTimeoutRef = useRef(null);

    // Clear expiration timeout
    const clearExpirationTimeout = () => {
        if (expirationTimeoutRef.current) {
            clearTimeout(expirationTimeoutRef.current);
            expirationTimeoutRef.current = null;
        }
    };

    const logout = useCallback(() => {
        clearExpirationTimeout();
        setToken(null);
        setUser(null);
        localStorage.removeItem('token');
    }, []);

    // Schedule proactive logout when token expires
    const scheduleLogout = useCallback((tokenValue) => {
        clearExpirationTimeout();

        if (!tokenValue) return;

        const timeoutId = scheduleTokenExpiration(tokenValue, () => {
            if (process.env.NODE_ENV === 'development') {
                console.warn('Token expired. Logging out...');
            }
            logout();
            // Redirect to login if not already there
            if (window.location.pathname !== '/login' && window.location.pathname !== '/register') {
                window.location.href = '/login';
            }
        });

        expirationTimeoutRef.current = timeoutId;
    }, [logout]);

    useEffect(() => {
        // Check for stored token on mount
        const storedToken = localStorage.getItem('token');
        if (storedToken) {
            // Validate token
            if (isTokenExpired(storedToken)) {
                if (process.env.NODE_ENV === 'development') {
                    console.warn('Stored token is expired. Clearing...');
                }
                localStorage.removeItem('token');
                setLoading(false);
                return;
            }

            // Decode token and extract user info
            const userInfo = getUserFromToken(storedToken);
            if (userInfo) {
                setToken(storedToken);
                setUser({
                    id: userInfo.id,
                    name: userInfo.name,
                    email: userInfo.email,
                    authenticated: true
                });

                // Schedule proactive logout
                scheduleLogout(storedToken);
            } else {
                // Invalid token format
                localStorage.removeItem('token');
            }
        }
        setLoading(false);

        // Cleanup on unmount
        return () => {
            clearExpirationTimeout();
        };
    }, [scheduleLogout]);

    const login = (authData) => {
        const tokenValue = authData.token;

        // Decode JWT to extract user info
        const userInfo = getUserFromToken(tokenValue);
        if (!userInfo) {
            if (process.env.NODE_ENV === 'development') {
                console.error('Failed to decode JWT token');
            }
            throw new Error('Invalid token format');
        }

        // Store only the token
        setToken(tokenValue);
        localStorage.setItem('token', tokenValue);

        // Set user info from decoded token
        setUser({
            id: userInfo.id,
            name: userInfo.name,
            email: userInfo.email,
            authenticated: true
        });

        // Schedule proactive logout at expiration
        scheduleLogout(tokenValue);
    };

    const value = {
        user,
        token,
        loading,
        login,
        logout,
        isAuthenticated: !!token,
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
};
