"use client"

import React from "react"

import { useState, useCallback } from "react"
import useSWR from "swr"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { ChevronLeft, ChevronRight, Search, Copy, Check, Loader2 } from "lucide-react"

interface Pagination {
  page: number
  limit: number
  totalCount: number
  totalPages: number
  hasNext: boolean
  hasPrev: boolean
}

interface SeedsResponse {
  seeds: Record<string, unknown>[]
  pagination: Pagination
}

const fetcher = (url: string) => fetch(url).then((res) => res.json())

function formatNumber(num: number): string {
  if (num >= 1_000_000_000) return `${(num / 1_000_000_000).toFixed(2)}B`
  if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(2)}M`
  if (num >= 1_000) return `${(num / 1_000).toFixed(1)}K`
  return num.toLocaleString()
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  const copy = useCallback(() => {
    navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }, [text])

  return (
    <button
      onClick={copy}
      className="p-1 rounded hover:bg-muted transition-colors"
      title="Copy seed"
    >
      {copied ? (
        <Check className="h-3.5 w-3.5 text-green-500" />
      ) : (
        <Copy className="h-3.5 w-3.5 text-muted-foreground" />
      )}
    </button>
  )
}

export function SeedTable() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState("")
  const [searchInput, setSearchInput] = useState("")
  const limit = 100

  const { data, error, isLoading } = useSWR<SeedsResponse>(
    `/api/seeds?page=${page}&limit=${limit}&search=${encodeURIComponent(search)}`,
    fetcher,
    { keepPreviousData: true }
  )

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault()
    setSearch(searchInput)
    setPage(1)
  }

  const columns = data?.seeds?.[0] ? Object.keys(data.seeds[0]) : []

  return (
    <div className="flex flex-col gap-4">
      {/* Search bar */}
      <form onSubmit={handleSearch} className="flex gap-2">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            type="text"
            placeholder="Search seeds..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-9 font-mono"
          />
        </div>
        <Button type="submit" variant="secondary">
          Search
        </Button>
        {search && (
          <Button
            type="button"
            variant="ghost"
            onClick={() => {
              setSearch("")
              setSearchInput("")
              setPage(1)
            }}
          >
            Clear
          </Button>
        )}
      </form>

      {/* Stats bar */}
      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <div className="flex items-center gap-4">
          {data?.pagination && (
            <>
              <span className="font-medium text-foreground">
                {formatNumber(data.pagination.totalCount)} seeds
              </span>
              <span>
                Page {data.pagination.page.toLocaleString()} of{" "}
                {data.pagination.totalPages.toLocaleString()}
              </span>
            </>
          )}
          {isLoading && (
            <span className="flex items-center gap-1.5">
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
              Loading...
            </span>
          )}
        </div>

        {/* Pagination */}
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage(1)}
            disabled={!data?.pagination?.hasPrev}
          >
            First
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="h-8 w-8 bg-transparent"
            onClick={() => setPage((p) => p - 1)}
            disabled={!data?.pagination?.hasPrev}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="h-8 w-8 bg-transparent"
            onClick={() => setPage((p) => p + 1)}
            disabled={!data?.pagination?.hasNext}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage(data?.pagination?.totalPages || 1)}
            disabled={!data?.pagination?.hasNext}
          >
            Last
          </Button>
        </div>
      </div>

      {/* Error state */}
      {error && (
        <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load seeds. Make sure your DuckDB is configured correctly.
          <pre className="mt-2 text-xs opacity-70">{JSON.stringify(error, null, 2)}</pre>
        </div>
      )}

      {/* Table */}
      <div className="rounded-lg border bg-card">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50">
              <TableHead className="w-10">#</TableHead>
              {columns.map((col) => (
                <TableHead key={col} className="font-semibold">
                  {col}
                </TableHead>
              ))}
              <TableHead className="w-10"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data?.seeds?.length === 0 && (
              <TableRow>
                <TableCell
                  colSpan={columns.length + 2}
                  className="h-32 text-center text-muted-foreground"
                >
                  {search ? "No seeds match your search" : "No seeds found"}
                </TableCell>
              </TableRow>
            )}
            {data?.seeds?.map((seed, idx) => {
              const rowNum = (page - 1) * limit + idx + 1
              const seedValue = String(seed[columns[0]] || "")
              return (
                <TableRow key={idx} className="font-mono text-xs">
                  <TableCell className="text-muted-foreground tabular-nums">
                    {rowNum.toLocaleString()}
                  </TableCell>
                  {columns.map((col) => (
                    <TableCell key={col} className="max-w-xs truncate">
                      {String(seed[col] ?? "")}
                    </TableCell>
                  ))}
                  <TableCell>
                    <CopyButton text={seedValue} />
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </div>

      {/* Bottom pagination */}
      {data?.pagination && data.pagination.totalPages > 1 && (
        <div className="flex items-center justify-center gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage(1)}
            disabled={!data.pagination.hasPrev}
          >
            First
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="h-8 w-8 bg-transparent"
            onClick={() => setPage((p) => p - 1)}
            disabled={!data.pagination.hasPrev}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="px-3 text-sm text-muted-foreground tabular-nums">
            {page} / {data.pagination.totalPages.toLocaleString()}
          </span>
          <Button
            variant="outline"
            size="icon"
            className="h-8 w-8 bg-transparent"
            onClick={() => setPage((p) => p + 1)}
            disabled={!data.pagination.hasNext}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage(data.pagination.totalPages)}
            disabled={!data.pagination.hasNext}
          >
            Last
          </Button>
        </div>
      )}
    </div>
  )
}
