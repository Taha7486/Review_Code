import React from 'react';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import { LogOut } from 'lucide-react';

const Dashboard = () => {
    const { logout } = useAuth();
    const navigate = useNavigate();

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    return (
        <div className="min-h-screen bg-gray-50">
            <nav className="bg-white shadow-sm">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex justify-between h-16 items-center">
                        <h1 className="text-2xl font-bold text-gray-900">Code Review Dashboard</h1>
                        <button
                            onClick={handleLogout}
                            className="flex items-center gap-2 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg transition-colors"
                        >
                            <LogOut className="w-4 h-4" />
                            Logout
                        </button>
                    </div>
                </div>
            </nav>

            <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
                <div className="bg-white rounded-lg shadow p-6">
                    <h2 className="text-xl font-semibold text-gray-900 mb-4">Welcome to Code Review Tool</h2>
                    <p className="text-gray-600">
                        You are now logged in! This is a placeholder dashboard. Future features will include:
                    </p>
                    <ul className="mt-4 space-y-2 text-gray-600">
                        <li>• View connected repositories</li>
                        <li>• Monitor pull requests</li>
                        <li>• Review code analysis results</li>
                        <li>• Configure review settings</li>
                    </ul>
                </div>
            </main>
        </div>
    );
};

export default Dashboard;
