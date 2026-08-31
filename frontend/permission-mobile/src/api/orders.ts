export {
  ApprovalStatus as BusinessApprovalStatus,
  getDemoBusinessOrders,
  getDemoBusinessOrder,
  createDemoBusinessOrder,
  updateDemoBusinessOrder,
  deleteDemoBusinessOrder,
  submitDemoBusinessOrder,
  withdrawDemoBusinessOrder,
  cancelDemoBusinessOrder,
  getDemoBusinessOrderAttachments,
  uploadDemoBusinessOrderAttachment,
  getDemoBusinessOrderChangeHistories,
  notifyDemoBusinessOrderOwner,
  exportDemoBusinessOrders,
} from './demoBusinessOrder'
export type {
  ApprovalStatus as BusinessApprovalStatusValue,
  DemoBusinessOrderQuery,
  DemoBusinessOrderItem,
  SaveDemoBusinessOrderRequest,
  DemoBusinessOrderChangeHistoryItem,
  DemoBusinessOrderPrintResult,
} from './demoBusinessOrder'
export {
  ApprovalStatus as DemoApprovalStatus,
  getDemoApprovalOrders,
  getDemoApprovalOrder,
  createDemoApprovalOrder,
  updateDemoApprovalOrder,
  deleteDemoApprovalOrder,
  submitDemoApprovalOrder,
  withdrawDemoApprovalOrder,
  cancelDemoApprovalOrder,
} from './demoApprovalOrder'
export type {
  ApprovalStatus as DemoApprovalStatusValue,
  DemoApprovalOrderQuery,
  DemoApprovalOrderItem,
  CreateDemoApprovalOrderRequest,
  UpdateDemoApprovalOrderRequest,
  SubmitDemoApprovalOrderRequest,
} from './demoApprovalOrder'

export type MobileOrderKind = 'business' | 'approval'
