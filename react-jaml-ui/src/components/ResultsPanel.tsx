import React from "react"
import { Table, Text, Badge, ScrollArea } from "@mantine/core"
import { useJamlStore } from "../store/JamlStore"

export function ResultsPanel() {
  const { searchState } = useJamlStore()
  
  const rows = searchState.results.map((result, index) => (
    <Table.Tr key={index}>
      <Table.Td>
        <Text size="sm" style={{ fontFamily: "monospace" }}>
          {result.seed}
        </Text>
      </Table.Td>
      <Table.Td>
        <Badge size="sm" color="green">
          {result.score?.toFixed(2) || "N/A"}
        </Badge>
      </Table.Td>
      <Table.Td>
        <Text size="sm">
          {result.description || "No description"}
        </Text>
      </Table.Td>
    </Table.Tr>
  ))
  
  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <ScrollArea h={400}>
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Seed</Table.Th>
              <Table.Th>Score</Table.Th>
              <Table.Th>Description</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {rows.length > 0 ? rows : (
              <Table.Tr>
                <Table.Td colSpan={3}>
                  <Text c="dimmed" ta="center" py="xl">
                    {searchState.isSearching ? "Searching..." : "No results yet"}
                  </Text>
                </Table.Td>
              </Table.Tr>
            )}
          </Table.Tbody>
        </Table>
      </ScrollArea>
      
      {searchState.status && (
        <Text size="sm" c="dimmed" mt="sm">
          Status: {searchState.status}
        </Text>
      )}
    </div>
  )
}
