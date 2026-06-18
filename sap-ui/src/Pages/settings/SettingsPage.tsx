import { useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent, Input, Select, Switch, Button } from '@/Components/ui'
import { useAppSelector } from '@/store/hooks'

const languageOptions = [
  { value: 'en', label: 'English' },
  { value: 'de', label: 'German' },
  { value: 'fr', label: 'French' },
  { value: 'es', label: 'Spanish' },
]

const timezoneOptions = [
  { value: 'utc', label: 'UTC' },
  { value: 'est', label: 'Eastern Time (EST)' },
  { value: 'pst', label: 'Pacific Time (PST)' },
  { value: 'cet', label: 'Central European Time (CET)' },
]

export function SettingsPage() {
  const user = useAppSelector((state) => state.auth.user)
  const [language, setLanguage] = useState('en')
  const [timezone, setTimezone] = useState('utc')
  const [emailNotifs, setEmailNotifs] = useState(true)
  const [darkMode, setDarkMode] = useState(false)

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Settings</h1>
        <p className="mt-1 text-sm text-slate-500">Manage your account and application preferences.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Profile</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <Input label="Full Name" defaultValue={user?.name} />
          <Input label="Email" type="email" defaultValue={user?.email} disabled />
          <Input label="Role" defaultValue={user?.role} disabled />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Preferences</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <Select
            label="Language"
            options={languageOptions}
            value={language}
            onChange={setLanguage}
          />
          <Select
            label="Timezone"
            options={timezoneOptions}
            value={timezone}
            onChange={setTimezone}
          />
          <Switch
            label="Email notifications"
            description="Receive updates about your account activity"
            checked={emailNotifs}
            onChange={(e) => setEmailNotifs(e.target.checked)}
          />
          <Switch
            label="Dark mode"
            description="Use dark theme across the application"
            checked={darkMode}
            onChange={(e) => setDarkMode(e.target.checked)}
          />
        </CardContent>
      </Card>

      <div className="flex justify-end">
        <Button>Save changes</Button>
      </div>
    </div>
  )
}
