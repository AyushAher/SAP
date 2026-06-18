import { createSlice, type PayloadAction } from '@reduxjs/toolkit'

interface UiSliceState {
  sidebarCollapsed: boolean
  sidebarMobileOpen: boolean
  theme: 'light' | 'dark'
}

const initialState: UiSliceState = {
  sidebarCollapsed: false,
  sidebarMobileOpen: false,
  theme: 'light',
}

const uiSlice = createSlice({
  name: 'ui',
  initialState,
  reducers: {
    toggleSidebar(state) {
      state.sidebarCollapsed = !state.sidebarCollapsed
    },
    setSidebarCollapsed(state, action: PayloadAction<boolean>) {
      state.sidebarCollapsed = action.payload
    },
    toggleMobileSidebar(state) {
      state.sidebarMobileOpen = !state.sidebarMobileOpen
    },
    setMobileSidebarOpen(state, action: PayloadAction<boolean>) {
      state.sidebarMobileOpen = action.payload
    },
    setTheme(state, action: PayloadAction<'light' | 'dark'>) {
      state.theme = action.payload
    },
  },
})

export const {
  toggleSidebar,
  setSidebarCollapsed,
  toggleMobileSidebar,
  setMobileSidebarOpen,
  setTheme,
} = uiSlice.actions

export default uiSlice.reducer
