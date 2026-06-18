import { useState } from 'react'
import {
  Button,
  Input,
  Textarea,
  Select,
  MultiSelect,
  Checkbox,
  Switch,
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  Badge,
  Modal,
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
} from '@/Components/ui'

const selectOptions = [
  { value: 'us', label: 'United States' },
  { value: 'uk', label: 'United Kingdom' },
  { value: 'de', label: 'Germany' },
  { value: 'fr', label: 'France' },
  { value: 'jp', label: 'Japan' },
]

const multiOptions = [
  { value: 'react', label: 'React' },
  { value: 'typescript', label: 'TypeScript' },
  { value: 'tailwind', label: 'Tailwind CSS' },
  { value: 'redux', label: 'Redux' },
  { value: 'echarts', label: 'ECharts' },
]

const tableData = [
  { id: '1', name: 'John Doe', email: 'john@company.com', role: 'Admin', status: 'Active' },
  { id: '2', name: 'Jane Smith', email: 'jane@company.com', role: 'Editor', status: 'Active' },
  { id: '3', name: 'Bob Wilson', email: 'bob@company.com', role: 'Viewer', status: 'Inactive' },
]

export function ComponentsPage() {
  const [selectValue, setSelectValue] = useState('')
  const [multiValue, setMultiValue] = useState<string[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [switchOn, setSwitchOn] = useState(false)

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">UI Components</h1>
        <p className="mt-1 text-sm text-slate-500">Enterprise-ready component library showcase.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Buttons</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-3">
          <Button>Primary</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="outline">Outline</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger">Danger</Button>
          <Button isLoading>Loading</Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Form Controls</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-6 md:grid-cols-2">
          <Input label="Text Input" placeholder="Enter text..." hint="This is a hint text" />
          <Input label="With Error" placeholder="Enter text..." error="This field is required" />
          <Textarea label="Textarea" placeholder="Enter description..." />
          <Select
            label="Select"
            options={selectOptions}
            value={selectValue}
            onChange={setSelectValue}
            placeholder="Choose a country"
            clearable
          />
          <MultiSelect
            label="Multi Select"
            options={multiOptions}
            value={multiValue}
            onChange={setMultiValue}
            placeholder="Choose technologies"
          />
          <div className="space-y-4">
            <Checkbox label="Accept terms and conditions" description="You agree to our terms of service" />
            <Switch
              label="Enable notifications"
              description="Receive email notifications"
              checked={switchOn}
              onChange={(e) => setSwitchOn(e.target.checked)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Badges</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Badge>Default</Badge>
          <Badge variant="primary">Primary</Badge>
          <Badge variant="success">Success</Badge>
          <Badge variant="warning">Warning</Badge>
          <Badge variant="danger">Danger</Badge>
          <Badge variant="outline">Outline</Badge>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Table</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tableData.map((row) => (
                <TableRow key={row.id}>
                  <TableCell className="font-medium">{row.name}</TableCell>
                  <TableCell>{row.email}</TableCell>
                  <TableCell>{row.role}</TableCell>
                  <TableCell>
                    <Badge variant={row.status === 'Active' ? 'success' : 'default'}>
                      {row.status}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Modal</CardTitle>
        </CardHeader>
        <CardContent>
          <Button onClick={() => setModalOpen(true)}>Open Modal</Button>
          <Modal
            isOpen={modalOpen}
            onClose={() => setModalOpen(false)}
            title="Confirm Action"
            description="Are you sure you want to proceed with this action?"
          >
            <div className="flex justify-end gap-3">
              <Button variant="outline" onClick={() => setModalOpen(false)}>
                Cancel
              </Button>
              <Button onClick={() => setModalOpen(false)}>Confirm</Button>
            </div>
          </Modal>
        </CardContent>
      </Card>
    </div>
  )
}
