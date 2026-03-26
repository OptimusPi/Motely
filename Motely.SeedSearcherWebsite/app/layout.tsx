import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Motely Seed Searcher",
  description: "Thin client for JAML-backed Balatro seed search and result review.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
