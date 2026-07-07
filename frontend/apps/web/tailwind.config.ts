import type { Config } from 'tailwindcss';
import colors from 'tailwindcss/colors';

const config: Config = {
  darkMode: ['class'],
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        // ChecaAI brand — blue (#2563EB) + dark navy, tech/startup identity
        primary: colors.blue,
        ink: colors.slate,
        success: colors.green,
        warning: colors.amber,
        danger: colors.red,
        alert: {
          normal:   '#4caf50',
          atencao:  '#ff9800',
          critico:  '#f44336',
        },
        vote: {
          yes:        '#4caf50',
          no:         '#f44336',
          abstention: '#ff9800',
          absent:     '#9e9e9e',
        },
      },
      fontFamily: {
        sans: ['var(--font-manrope)', 'system-ui', 'sans-serif'],
      },
      keyframes: {
        'fade-scale-in': {
          '0%': { opacity: '0', transform: 'scale(0.98)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
      },
      animation: {
        'fade-scale-in': 'fade-scale-in 150ms ease-out',
      },
    },
  },
  plugins: [],
};

export default config;
