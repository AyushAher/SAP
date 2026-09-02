import { Navigate, useParams } from 'react-router-dom'
import { productionOrderSubassemblyPath } from '@/config/constants'

/** Items are edited on the same screen as the sub-assembly header. */
export function SubassemblyItemsPage() {
  const { id, childId } = useParams()
  if (!id || !childId) return <Navigate to="/" replace />
  return <Navigate to={productionOrderSubassemblyPath(id, childId)} replace />
}
