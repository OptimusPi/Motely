import type { Metadata } from "next";
import "jaml-ui/jimbo.css";
import "./globals.css";

export const metadata: Metadata = {
  title: "Balatro Seed Lab — Find, Analyze & Compare Seeds",
  description:
    "AI-powered Balatro seed finder. Search 2.3 trillion seeds with JAML filters, analyze full routes, and discover erratic deck compositions — all in your browser.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className="min-h-screen antialiased">{children}</body>
    </html>
  );
}
