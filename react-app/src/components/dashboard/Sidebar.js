import React from 'react';
import { LogOut, Search, Clock, CheckCircle } from 'lucide-react';

const Sidebar = ({ view, setView, onLogout }) => {
    return (
        <aside className="fixed inset-y-0 left-0 w-64 bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 shadow-sm z-30 hidden md:flex flex-col transition-colors">
            <div className="p-6 border-b border-gray-200 dark:border-gray-700 flex items-center gap-3">
                <div className="bg-indigo-600 p-2 rounded-lg">
                    <CheckCircle className="w-6 h-6 text-white" />
                </div>
                <h1 className="text-xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-indigo-600 to-violet-600 dark:from-indigo-400 dark:to-violet-400">
                    CodeReviewAI
                </h1>
            </div>

            <nav className="flex-1 p-4 space-y-2">
                <button
                    onClick={() => setView('analyze')}
                    className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all ${view === 'analyze'
                            ? 'bg-indigo-50 dark:bg-indigo-900/20 text-indigo-600 dark:text-indigo-400 font-medium shadow-sm'
                            : 'text-gray-600 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-700/50'
                        }`}
                >
                    <Search className="w-5 h-5" />
                    New Analysis
                </button>
                <button
                    onClick={() => setView('history')}
                    className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all ${view === 'history'
                            ? 'bg-indigo-50 dark:bg-indigo-900/20 text-indigo-600 dark:text-indigo-400 font-medium shadow-sm'
                            : 'text-gray-600 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-700/50'
                        }`}
                >
                    <Clock className="w-5 h-5" />
                    History
                </button>
            </nav>

            <div className="p-4 border-t border-gray-200 dark:border-gray-700">
                <button
                    onClick={onLogout}
                    className="w-full flex items-center gap-3 px-4 py-2 rounded-lg text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/10 transition-colors"
                >
                    <LogOut className="w-5 h-5" />
                    Sign Out
                </button>
            </div>
        </aside>
    );
};

export default Sidebar;
