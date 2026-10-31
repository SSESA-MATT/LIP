/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./app/**/*.{js,ts,jsx,tsx}", "./components/**/*.{js,ts,jsx,tsx}", "./lib/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        navy: { DEFAULT: "#1e3a5f", deep: "#1e3a5f", slate: "#2d5a87" },
        primary: { blue: "#2d5a87", violet: "#4a7fb5" },
        surface: { DEFAULT: "#f5f7fa", cool: "#e2e8f0", lavender: "#e8f0fe", green: "#ecfdf5", orange: "#fffbeb" },
        success: "#059669",
        warning: "#d97706",
        error: "#dc2626",
        text: { primary: "#1a1a2e", secondary: "#64748b" },
      },
      fontFamily: { sans: ["Inter", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "Roboto", "sans-serif"] },
      fontSize: {
        h1: ["32px", { lineHeight: "1.2", fontWeight: "700" }],
        h2: ["24px", { lineHeight: "1.3", fontWeight: "600" }],
        h3: ["20px", { lineHeight: "1.3", fontWeight: "600" }],
        body: ["16px", { lineHeight: "1.5" }],
        secondary: ["14px", { lineHeight: "1.5" }],
        caption: ["12px", { lineHeight: "1.4" }],
      },
      borderRadius: { glass: "16px", card: "12px", pill: "999px" },
      boxShadow: {
        glass: "0 1px 3px rgba(0, 0, 0, 0.04), 0 1px 2px rgba(0, 0, 0, 0.02)",
        elevated: "0 4px 12px rgba(0, 0, 0, 0.05)",
      },
    },
  },
  plugins: [],
};
