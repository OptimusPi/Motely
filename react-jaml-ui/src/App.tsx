import React from "react"
import { MantineProvider } from "@mantine/core"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { CompleteJamlUI } from "./components/CompleteJamlUI"

const queryClient = new QueryClient()

function App() {
  return (
    <MantineProvider defaultColorScheme="dark">
      <QueryClientProvider client={queryClient}>
        <CompleteJamlUI />
      </QueryClientProvider>
    </MantineProvider>
  )
}

export default App
