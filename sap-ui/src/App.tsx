import { RouterProvider } from 'react-router-dom'
import { ApiLoadingOverlay } from '@/Components/shared/ApiLoadingOverlay'
import { router } from '@/routes'

function App() {
  return (
    <>
      <ApiLoadingOverlay />
      <RouterProvider router={router} />
    </>
  )
}

export default App
