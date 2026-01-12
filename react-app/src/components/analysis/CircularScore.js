import React from 'react';

const CircularScore = ({ score, size = 'lg' }) => {
    const radius = size === 'lg' ? 50 : 36;
    const circumference = 2 * Math.PI * radius;
    const offset = circumference - (score / 100) * circumference;

    let color = 'text-green-500 dark:text-green-400';
    let glowColor = 'rgba(74, 222, 128, 0.5)'; // green
    if (score < 50) {
        color = 'text-red-500 dark:text-red-400';
        glowColor = 'rgba(248, 113, 113, 0.5)'; // red
    } else if (score < 80) {
        color = 'text-yellow-500 dark:text-yellow-400';
        glowColor = 'rgba(250, 204, 21, 0.5)'; // yellow
    }

    const sizeClass = size === 'lg' ? 'w-40 h-40' : 'w-28 h-28';
    const textSize = size === 'lg' ? 'text-4xl' : 'text-3xl';
    const strokeWidth = size === 'lg' ? 8 : 6;

    return (
        <div className={`relative ${sizeClass} flex items-center justify-center`}>
            <div
                className="absolute inset-0 rounded-full blur-xl opacity-20"
                style={{ backgroundColor: score >= 80 ? '#4ade80' : score < 50 ? '#f87171' : '#facc15' }}
            />
            <svg className="w-full h-full transform -rotate-90 relative z-10">
                <circle
                    cx="50%"
                    cy="50%"
                    r={radius}
                    stroke="currentColor"
                    strokeWidth={strokeWidth}
                    fill="transparent"
                    className="text-gray-200 dark:text-gray-700"
                />
                <circle
                    cx="50%"
                    cy="50%"
                    r={radius}
                    stroke="currentColor"
                    strokeWidth={strokeWidth}
                    fill="transparent"
                    strokeDasharray={circumference}
                    strokeDashoffset={offset}
                    className={`${color} transition-all duration-1000 ease-out`}
                    strokeLinecap="round"
                    style={{ filter: `drop-shadow(0 0 4px ${glowColor})` }}
                />
            </svg>
            <div className="absolute flex flex-col items-center z-10">
                <span className={`${textSize} font-bold ${color}`}>{score}</span>
                <span className="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Score</span>
            </div>
        </div>
    );
};

export default CircularScore;
