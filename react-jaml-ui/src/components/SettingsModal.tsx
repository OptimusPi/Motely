import React from "react"
import { Modal, Select, Button, Group, Text, List, ActionIcon } from "@mantine/core"
import { IconTrash } from "@tabler/icons-react"
import { useJamlStore } from "../store/JamlStore"

interface SettingsModalProps {
  opened: boolean
}

export function SettingsModal({ opened }: SettingsModalProps) {
  const { 
    jamlState, 
    setSettingsOpen, 
    setCurrentFilter, 
    deleteFilter,
    setJamlContent 
  } = useJamlStore()
  
  const handleFilterSelect = (filterId: string) => {
    const filter = jamlState.filters.find(f => f.id === filterId)
    if (filter) {
      setJamlContent(filter.content)
      setCurrentFilter(filterId)
    }
  }
  
  const handleDeleteFilter = (filterId: string) => {
    deleteFilter(filterId)
  }
  
  return (
    <Modal
      opened={opened}
      onClose={() => setSettingsOpen(false)}
      title="JAML Filter Settings"
      size="md"
    >
      <Select
        label="Load Filter"
        placeholder="Select a filter to load"
        data={jamlState.filters.map(filter => ({
          value: filter.id,
          label: filter.name
        }))}
        value={jamlState.currentFilter}
        onChange={(value) => value && handleFilterSelect(value)}
        mb="md"
      />
      
      <Text size="sm" fw={500} mb="xs">
        Saved Filters ({jamlState.filters.length})
      </Text>
      
      <List spacing="xs" size="sm">
        {jamlState.filters.map(filter => (
          <List.Item key={filter.id}>
            <Group justify="space-between">
              <div>
                <Text size="sm">{filter.name}</Text>
                <Text size="xs" c="dimmed">
                  {new Date(filter.created).toLocaleString()}
                </Text>
              </div>
              <ActionIcon
                size="sm"
                color="red"
                variant="subtle"
                onClick={() => handleDeleteFilter(filter.id)}
              >
                <IconTrash size={16} />
              </ActionIcon>
            </Group>
          </List.Item>
        ))}
      </List>
      
      {jamlState.filters.length === 0 && (
        <Text c="dimmed" ta="center" py="md">
          No saved filters yet. Create and save a filter to see it here.
        </Text>
      )}
    </Modal>
  )
}
