
namespace LogJoint.Drawing
{
	partial class StringFormat
	{
		internal StringAlignment horizontalAlignment;
		internal StringAlignment verticalAlignment;
		internal LineBreakMode lineBreakMode;

		partial void Init(StringAlignment horizontalAlignment, StringAlignment verticalAlignment,
			LineBreakMode lineBreakMode)
		{
			this.horizontalAlignment = horizontalAlignment;
			this.verticalAlignment = verticalAlignment;
			this.lineBreakMode = lineBreakMode;
		}
	};
}