import { render, screen } from '@testing-library/react';
import CircularScore from '../CircularScore';

// Mocks to prevent issues with SVGs or complex styles if necessary
// But here the component is simple enough to render directly

describe('CircularScore Component', () => {
    test('renders score correctly', () => {
        render(<CircularScore score={85} />);
        expect(screen.getByText('85')).toBeInTheDocument();
        expect(screen.getByText('Score')).toBeInTheDocument();
    });

    test('applies green color for high scores', () => {
        render(<CircularScore score={90} />);
        const scoreText = screen.getByText('90');
        expect(scoreText).toHaveClass('text-green-500');
    });

    test('applies yellow color for medium scores', () => {
        render(<CircularScore score={75} />);
        const scoreText = screen.getByText('75');
        expect(scoreText).toHaveClass('text-yellow-500');
    });

    test('applies red color for low scores', () => {
        render(<CircularScore score={40} />);
        const scoreText = screen.getByText('40');
        expect(scoreText).toHaveClass('text-red-500');
    });

    test('renders with custom size', () => {
        const { container } = render(<CircularScore score={85} size="sm" />);
        // Checking if class is applied to the wrapper div is tricky depending on structure
        // But we can check if the text size class is different
        // In the code: const textSize = size === 'lg' ? 'text-4xl' : 'text-3xl';
        const scoreText = screen.getByText('85');
        expect(scoreText).toHaveClass('text-3xl');
    });
});
