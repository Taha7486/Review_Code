import React from 'react';
import { FileText, FileCode, FileJson, Image, Database, Coffee } from 'lucide-react';

const getFileIcon = (filename) => {
    const ext = filename?.split('.').pop().toLowerCase() || '';
    switch (ext) {
        case 'js':
        case 'jsx':
        case 'ts':
        case 'tsx':
            return <FileCode className="w-4 h-4 text-yellow-500" />;
        case 'html':
        case 'css':
            return <FileCode className="w-4 h-4 text-orange-500" />;
        case 'json':
        case 'yml':
        case 'yaml':
        case 'xml':
            return <FileJson className="w-4 h-4 text-blue-500" />;
        case 'php':
            return <Database className="w-4 h-4 text-indigo-500" />;
        case 'py':
            return <Coffee className="w-4 h-4 text-green-500" />;
        case 'png':
        case 'jpg':
        case 'jpeg':
        case 'svg':
            return <Image className="w-4 h-4 text-purple-500" />;
        default:
            return <FileText className="w-4 h-4 text-gray-500" />;
    }
};

const FileIcon = ({ filename }) => {
    return getFileIcon(filename);
};

export default FileIcon;
