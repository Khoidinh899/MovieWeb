// Theme tokens merged from the Stitch exports (stitch-theme.json, same values as DESIGN.md).
// Use these tokens (bg-surface, text-on-surface, font-headline-lg...) instead of literal hex colors.
const stitch = require('./stitch-theme.json');

/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: ['../Views/V2/**/*.cshtml', '../wwwroot/v2/js/**/*.js'],
  theme: stitch.theme,
  // Bootstrap's .collapse is a state class toggled by Bootstrap JS; Tailwind's .collapse (visibility: collapse) would hide it.
  blocklist: ['collapse'],
  plugins: [require('@tailwindcss/forms'), require('@tailwindcss/container-queries')],
};
