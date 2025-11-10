/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        background: 'hsl(222.2 84% 4.9%)',
        foreground: 'hsl(210 40% 98%)',
        card: 'hsl(222.2 84% 4.9%)',
        'card-foreground': 'hsl(210 40% 98%)',
        primary: 'hsl(217.2 91.2% 59.8%)',
        'primary-foreground': 'hsl(222.2 47.4% 11.2%)',
        secondary: 'hsl(217.2 32.6% 17.5%)',
        'secondary-foreground': 'hsl(210 40% 98%)',
        accent: 'hsl(217.2 91.2% 59.8%)',
        'accent-foreground': 'hsl(222.2 47.4% 11.2%)',
        success: 'hsl(142.1 76.2% 36.3%)',
        warning: 'hsl(47.9 95.8% 53.1%)',
        error: 'hsl(0 84.2% 60.2%)',
        border: 'hsl(217.2 32.6% 17.5%)',
        input: 'hsl(217.2 32.6% 17.5%)',
        ring: 'hsl(217.2 91.2% 59.8%)',
      },
      borderRadius: {
        lg: '12px',
        md: '8px',
        sm: '6px',
      },
    },
  },
  plugins: [],
}
