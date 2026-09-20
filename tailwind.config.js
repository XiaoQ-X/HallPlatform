/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./index.html', './src/**/*.{vue,js}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Noto Sans SC"', 'system-ui', 'sans-serif']
      },
      colors: {
        brand: {
          50: '#effcf9', 100: '#c7f6ec', 200: '#94ecda', 300: '#58dcc4',
          400: '#2ac4aa', 500: '#10a890', 600: '#078775', 700: '#066c5f',
          800: '#09564d', 900: '#0a4740'
        },
        ink: {
          50: '#f5f7fa', 100: '#e9edf3', 200: '#d3dbe6', 300: '#aebccd',
          400: '#7e92ab', 500: '#5d728e', 600: '#485a74', 700: '#3b495e',
          800: '#333f50', 900: '#1f2937'
        }
      },
      boxShadow: {
        soft: '0 4px 20px -4px rgba(15, 60, 80, 0.10)',
        lift: '0 14px 40px -12px rgba(8, 70, 80, 0.22)',
        glow: '0 8px 30px -6px rgba(16, 168, 144, 0.45)'
      },
      borderRadius: { '4xl': '2rem' },
      keyframes: {
        floaty: { '0%,100%': { transform: 'translateY(0)' }, '50%': { transform: 'translateY(-8px)' } },
        fadeUp: { '0%': { opacity: 0, transform: 'translateY(14px)' }, '100%': { opacity: 1, transform: 'translateY(0)' } },
        pop: { '0%': { opacity: 0, transform: 'scale(.96)' }, '100%': { opacity: 1, transform: 'scale(1)' } }
      },
      animation: {
        floaty: 'floaty 5s ease-in-out infinite',
        fadeUp: 'fadeUp .5s ease both',
        pop: 'pop .3s ease both'
      }
    }
  },
  plugins: []
}
