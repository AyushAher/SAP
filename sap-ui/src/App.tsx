import { RouterProvider } from 'react-router-dom'
import { ApiLoadingOverlay } from '@/Components/shared/ApiLoadingOverlay'
import { ToastHost } from '@/Components/shared/ToastHost'
import { router } from '@/routes'

function App() {
  return (
    <>
      <ApiLoadingOverlay />
      <ToastHost />
      <RouterProvider router={router} />
    </>
  )
}

export default App
