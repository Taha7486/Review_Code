import React, { createContext, useContext, useState, useEffect } from 'react';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        // Check for stored token on mount
        const storedToken = localStorage.getItem('token');
        if (storedToken) {
            setToken(storedToken);
            // In a real app, you'd decode the JWT to get user info
            // For now, we'll just mark as authenticated
        }
        setLoading(false);
    }, []);

    const login = (authData) => {
        setToken(authData.token);
        localStorage.setItem('token', authData.token);
        // You could decode JWT here to set user data
        setUser({ authenticated: true });
    };

    const logout = () => {
        setToken(null);
        setUser(null);
        localStorage.removeItem('token');
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
