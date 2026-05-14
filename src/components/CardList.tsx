"use client";

import React from "react";

export interface CardListProps {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
  className?: string;
}

export function CardList({ title, subtitle, children, className = "" }: CardListProps) {
  return (
    <div className={className}>
      <div style={{ marginBottom: 4 }}>
        <span style={{ fontSize: 12 }}>{title}</span>
        {subtitle && <span style={{ fontSize: 10, opacity: 0.6, marginLeft: 4 }}>{subtitle}</span>}
      </div>
      <div style={{ display: "flex", gap: 4, overflowX: "auto", paddingBottom: 4 }}>
        {children}
      </div>
    </div>
  );
}
