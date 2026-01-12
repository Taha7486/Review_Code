import React from 'react';

const SeverityBadge = ({ severity, size = 'sm' }) => {
    const sizeClasses = size === 'sm' ? 'text-xs px-2.5 py-0.5' : 'text-sm px-3 py-1';
    const colors = {
        critical: 'bg-red-500/10 text-red-600 dark:text-red-400 border-red-200 dark:border-red-900',
        major: 'bg-orange-500/10 text-orange-600 dark:text-orange-400 border-orange-200 dark:border-orange-900',
        minor: 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 border-yellow-200 dark:border-yellow-900',
        info: 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-200 dark:border-blue-900'
    };
    const color = colors[severity?.toLowerCase()] || colors.info;

    return (
        <span className={`${sizeClasses} ${color} border rounded-full font-medium uppercase tracking-wider shadow-sm`}>
            {severity || 'info'}
        </span>
    );
};

export default SeverityBadge;
