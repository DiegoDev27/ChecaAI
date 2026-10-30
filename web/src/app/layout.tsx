import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "ChecaAI — dados públicos do Estado brasileiro",
  description: "Consulte, cruze e confira os dados públicos do Estado brasileiro, sempre com a fonte oficial.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <body className="min-h-screen font-sans antialiased">{children}</body>
    </html>
  );
}
